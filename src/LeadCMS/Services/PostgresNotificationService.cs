// <copyright file="PostgresNotificationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LeadCMS.Services;

/// <summary>
/// Service that listens for PostgreSQL NOTIFY events and manages change notifications.
/// </summary>
public class PostgresNotificationService : IHostedService, IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly SseClientManager clientManager;
    private readonly ILogger<PostgresNotificationService> logger;
    private readonly IConfiguration configuration;
    private readonly IMapper mapper;
    private readonly HashSet<Type> supportedTypes;

    private NpgsqlConnection? notificationConnection;
    private Task? listeningTask;
    private Timer? pollingTimer;
    private Timer? draftPollingTimer;
    private CancellationTokenSource? cancellationTokenSource;
    private CancellationTokenSource? listeningCancellationTokenSource;
    private bool isListening = false;
    private int lastPolledChangeLogId = 0;

    public PostgresNotificationService(
        IServiceProvider serviceProvider,
        SseClientManager clientManager,
        ILogger<PostgresNotificationService> logger,
        IConfiguration configuration,
        IMapper mapper)
    {
        this.serviceProvider = serviceProvider;
        this.clientManager = clientManager;
        this.logger = logger;
        this.configuration = configuration;
        this.mapper = mapper;
        supportedTypes = GetSupportedTypes();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationTokenSource = new CancellationTokenSource();

        // Start monitoring client connections
        _ = Task.Run(MonitorClientConnections, cancellationToken);

        logger.LogInformation("PostgresNotificationService started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopListening();
        cancellationTokenSource?.Cancel();
        logger.LogInformation("PostgresNotificationService stopped");
    }

    public void Dispose()
    {
        try
        {
            StopAsync(CancellationToken.None).Wait();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during disposal");
        }
        finally
        {
            cancellationTokenSource?.Dispose();
            listeningCancellationTokenSource?.Dispose();
            notificationConnection?.Dispose();
            pollingTimer?.Dispose();
        }
    }

    /// <summary>
    /// Monitor SSE client connections and manage LISTEN/polling lifecycle.
    /// </summary>
    private async Task MonitorClientConnections()
    {
        while (!cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                var clientCount = clientManager.ConnectedClientCount;

                if (clientCount > 0 && !isListening)
                {
                    // First client connected - start listening
                    logger.LogInformation("Starting PostgreSQL listening due to {ClientCount} connected SSE clients", clientCount);
                    await StartListening();
                }
                else if (clientCount == 0 && isListening)
                {
                    // Last client disconnected - stop listening
                    logger.LogInformation("Stopping PostgreSQL listening due to no connected SSE clients");
                    await StopListening();
                }

                await Task.Delay(1000, cancellationTokenSource.Token); // Check every second
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in client connection monitoring");
                await Task.Delay(5000, cancellationTokenSource.Token); // Wait before retry
            }
        }
    }

    /// <summary>
    /// Start PostgreSQL LISTEN and polling.
    /// </summary>
    private async Task StartListening()
    {
        if (isListening)
        {
            return;
        }

        try
        {
            // Get current minimum lastChangeLogId from all connected clients
            // This determines what changes we need to poll for
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            var minClientLastId = clientManager.GetMinimumLastChangeLogId();
            if (minClientLastId.HasValue)
            {
                lastPolledChangeLogId = minClientLastId.Value;
            }
            else
            {
                // No clients yet, set to current max to avoid processing historical data
                var supportedTypeNames = supportedTypes.Select(t => t.Name).ToList();
                lastPolledChangeLogId = await dbContext.ChangeLogs!
                    .Where(cl => supportedTypeNames.Contains(cl.ObjectType))
                    .MaxAsync(cl => (int?)cl.Id, cancellationTokenSource!.Token) ?? 0;
            }

            // Ensure any existing connection is properly closed
            if (notificationConnection != null)
            {
                try
                {
                    await notificationConnection.CloseAsync();
                    notificationConnection.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error closing existing notification connection");
                }

                notificationConnection = null;
            }

            // Setup PostgreSQL NOTIFY listener
            var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                                   BuildConnectionString();

            notificationConnection = new NpgsqlConnection(connectionString);
            await notificationConnection.OpenAsync(cancellationTokenSource!.Token);

            notificationConnection.Notification += OnNotificationReceived;

            using (var cmd = new NpgsqlCommand("LISTEN entity_changes", notificationConnection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationTokenSource.Token);
            }

            using (var cmd = new NpgsqlCommand("LISTEN draft_changes", notificationConnection)) // Listen for drafts
            {
                await cmd.ExecuteNonQueryAsync(cancellationTokenSource.Token);
            }

            logger.LogInformation("Successfully executed LISTEN entity_changes and draft_changes commands");

            // Create separate cancellation token for listening task
            listeningCancellationTokenSource = new CancellationTokenSource();

            // Start background listening task with separate cancellation token
            listeningTask = Task.Run(() => ListenForNotifications(listeningCancellationTokenSource.Token), cancellationTokenSource.Token);

            // Start polling timers as Plan B (every 5 seconds)
            pollingTimer = new Timer(async _ => await PollForChanges(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            draftPollingTimer = new Timer(async _ => await PollForDraftChanges(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            isListening = true;
            logger.LogInformation("Started PostgreSQL LISTEN and polling. Baseline ChangeLog ID: {LastId}", lastPolledChangeLogId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start PostgreSQL listening");
            await StopListening();
        }
    }

    /// <summary>
    /// Stop PostgreSQL LISTEN and polling.
    /// </summary>
    private async Task StopListening()
    {
        if (!isListening)
        {
            return;
        }

        try
        {
            isListening = false;

            // Stop polling timers
            pollingTimer?.Dispose();
            pollingTimer = null;

            draftPollingTimer?.Dispose();
            draftPollingTimer = null;

            // Cancel the listening task (cancels WaitAsync)
            if (listeningCancellationTokenSource != null && !listeningCancellationTokenSource.IsCancellationRequested)
            {
                listeningCancellationTokenSource.Cancel();
            }

            // Wait for listening task to finish
            if (listeningTask != null)
            {
                try
                {
                    await listeningTask;
                }
                catch
                {
                    /* ignore */
                }
                finally
                {
                    listeningTask = null;
                }
            }
            
            // Now it is safe to execute commands and close the connection
            if (notificationConnection != null)
            {
                try
                {
                    // Unlisten from both channels
                    using var cmd1 = new NpgsqlCommand("UNLISTEN entity_changes", notificationConnection);
                    await cmd1.ExecuteNonQueryAsync();

                    using var cmd2 = new NpgsqlCommand("UNLISTEN draft_changes", notificationConnection);
                    await cmd2.ExecuteNonQueryAsync();

                    logger.LogInformation("Successfully executed UNLISTEN commands for both entity_changes and draft_changes");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error sending UNLISTEN commands");
                }

                try
                {
                    notificationConnection.Notification -= OnNotificationReceived;
                    await notificationConnection.CloseAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error closing notification connection");
                }
                finally
                {
                    notificationConnection.Dispose();
                    notificationConnection = null;
                }
            }

            // Dispose of the listening cancellation token source
            listeningCancellationTokenSource?.Dispose();
            listeningCancellationTokenSource = null;

            logger.LogInformation("Stopped PostgreSQL LISTEN and polling");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping PostgreSQL listening");
        }
    }

    /// <summary>
    /// Background task that waits for PostgreSQL notifications.
    /// </summary>
    private async Task ListenForNotifications(CancellationToken cancellationToken)
    {
        try
        {
            while (isListening && !cancellationToken.IsCancellationRequested)
            {
                if (notificationConnection != null && notificationConnection.State == System.Data.ConnectionState.Open)
                {
                    await notificationConnection.WaitAsync(cancellationToken);
                }
                else
                {
                    // Connection is closed or null, wait a bit before retrying
                    logger.LogWarning("PostgreSQL notification connection is not available, waiting before retry");
                    await Task.Delay(5000, cancellationToken);

                    // Try to restart listening if connection is lost
                    if (isListening)
                    {
                        logger.LogInformation("Attempting to restart PostgreSQL listening due to connection loss");
                        await StopListening();
                        await StartListening();
                    }

                    break; // Exit the loop to let the new listening task take over
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in PostgreSQL notification listening, will attempt restart");
            
            // Try to restart listening on error
            if (isListening && cancellationTokenSource != null && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, cancellationToken);
                    await StopListening();
                    await StartListening();
                }
                catch (Exception restartEx)
                {
                    logger.LogError(restartEx, "Failed to restart PostgreSQL listening after error");
                }
            }
        }
    }

    /// <summary>
    /// Handle PostgreSQL NOTIFY events.
    /// </summary>
    private void OnNotificationReceived(object sender, NpgsqlNotificationEventArgs e)
    {
        logger.LogInformation("[SSE] PostgreSQL notification received: channel={Channel}, payload={Payload}", e.Channel, e.Payload);

        if (e.Channel == "entity_changes")
        {
            logger.LogInformation("[SSE] NOTIFY entity_changes received, triggering PollForChanges()");
            _ = Task.Run(async () => await PollForChanges());
        }
        else if (e.Channel == "draft_changes")
        {
            logger.LogInformation("[SSE] NOTIFY draft_changes received, triggering PollForDraftChanges()");
            _ = Task.Run(async () => await PollForDraftChanges());
        }
    }

    /// <summary>
    /// Poll for new draft changes and notify subscribed clients.
    /// </summary>
    private async Task PollForDraftChanges()
    {
        logger.LogInformation("[SSE] PollForDraftChanges called. isListening={IsListening}, clientCount={ClientCount}", isListening, clientManager.ConnectedClientCount);
        if (!isListening || clientManager.ConnectedClientCount == 0)
        {
            logger.LogInformation("[SSE] Skipping draft polling: isListening={IsListening}, clientCount={ClientCount}", isListening, clientManager.ConnectedClientCount);
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            // Get all clients subscribed to draft updates
            var draftClients = clientManager.GetClients()
                .Where(c => c.IncludeLiveDrafts)
                .ToList();

            if (!draftClients.Any())
            {
                return;
            }

            // Find the minimum LastDraftUpdateAt among all clients (default to DateTime.MinValue)
            var minLastDraftUpdateAt = draftClients.Min(c => c.LastDraftUpdateAt ?? DateTime.UtcNow);

            // Pull all drafts changed since minLastDraftUpdateAt
            var allDrafts = await dbContext.ContentDrafts!
                .Where(d => (d.UpdatedAt ?? d.CreatedAt) > minLastDraftUpdateAt)
                .OrderBy(d => d.UpdatedAt ?? d.CreatedAt)
                .ToListAsync();

            logger.LogInformation("[SSE] Found {DraftCount} changed drafts since {MinLastDraftUpdateAt}", allDrafts.Count, minLastDraftUpdateAt);

            foreach (var client in draftClients)
            {
                logger.LogInformation("[SSE] Processing draft notifications for client {ClientId}, LastDraftUpdateAt={LastDraftUpdateAt}", client.ClientId, client.LastDraftUpdateAt);

                var clientLastDraftUpdateAt = client.LastDraftUpdateAt ?? DateTime.UtcNow;

                var draftsForClient = allDrafts
                    .Where(d => (d.UpdatedAt ?? d.CreatedAt) > clientLastDraftUpdateAt)
                    .ToList();

                DateTime? maxSent = null;

                foreach (var draft in draftsForClient)
                {
                    logger.LogInformation("[SSE] Sending draft notification to client {ClientId}: ObjectType={ObjectType}, ObjectId={ObjectId}, CreatedById={CreatedById}, UpdatedAt={UpdatedAt}", client.ClientId, draft.ObjectType, draft.ObjectId, draft.CreatedById, draft.UpdatedAt ?? draft.CreatedAt);
                    await clientManager.SendDraftNotificationAsync(
                        client,
                        draft.CreatedById!,
                        draft.ObjectType,
                        draft.ObjectId,
                        draft.UpdatedAt ?? draft.CreatedAt,
                        draft.Data);

                    var thisDraftAt = draft.UpdatedAt ?? draft.CreatedAt;

                    if (maxSent == null || thisDraftAt > maxSent)
                    {
                        maxSent = thisDraftAt;
                    }
                }

                if (maxSent != null)
                {
                    client.LastDraftUpdateAt = maxSent;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error polling for draft changes");
        }
    }

    /// <summary>
    /// Poll for new ChangeLog entries (Plan B and NOTIFY handler).
    /// </summary>
    private async Task PollForChanges()
    {
        logger.LogInformation("[SSE] PollForChanges called. isListening={IsListening}, clientCount={ClientCount}", isListening, clientManager.ConnectedClientCount);
        if (!isListening || clientManager.ConnectedClientCount == 0)
        {
            logger.LogInformation("[SSE] Skipping polling: isListening={IsListening}, clientCount={ClientCount}", isListening, clientManager.ConnectedClientCount);
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            // Get all clients
            var clients = clientManager.GetClients().ToList();

            if (!clients.Any())
            {
                logger.LogDebug("No clients connected, skipping polling");
                return;
            }

            // Find the minimum LastChangeLogId among all clients
            var minLastChangeLogId = clients.Min(c => c.LastChangeLogId);

            logger.LogInformation("[SSE] Polling for changes since ID {MinClientLastId}", minLastChangeLogId);

            // Get supported type names for database query
            var supportedTypeNames = supportedTypes.Select(t => t.Name).ToList();

            // Pull all ChangeLog entries since minLastChangeLogId
            var allChanges = await dbContext.ChangeLogs!
                .Where(cl => cl.Id > minLastChangeLogId && supportedTypeNames.Contains(cl.ObjectType))
                .OrderBy(cl => cl.Id)
                .Take(500) // Process in batches
                .ToListAsync();

            foreach (var client in clients)
            {
                logger.LogInformation("[SSE] Processing change notifications for client {ClientId}, LastChangeLogId={LastChangeLogId}", client.ClientId, client.LastChangeLogId);

                var clientLastChangeLogId = client.LastChangeLogId;

                var changesForClient = allChanges
                    .Where(cl => cl.Id > clientLastChangeLogId)
                    .ToList();

                int? maxSent = null;

                // Group by entity type for efficient data fetching
                var grouped = changesForClient.GroupBy(cl => cl.ObjectType).ToList();

                foreach (var group in grouped)
                {
                    logger.LogInformation("[SSE] Processing entity type {EntityType} for client {ClientId}", group.Key, client.ClientId);

                    // Get entity data for content subscribers (skip deleted entities)
                    var entityDataMap = new Dictionary<int, object>();

                    var nonDeletedChanges = group.Where(cl => cl.EntityState != EntityState.Deleted).ToList();

                    if (nonDeletedChanges.Any())
                    {
                        var entityIds = nonDeletedChanges.Select(cl => cl.ObjectId).Distinct().ToList();
                        entityDataMap = await GetEntityData(dbContext, group.Key, entityIds);
                    }

                    foreach (var change in group)
                    {
                        logger.LogInformation("[SSE] Sending change notification to client {ClientId}: EntityType={EntityType}, EntityId={EntityId}, ChangeLogId={ChangeLogId}, Operation={Operation}, Timestamp={Timestamp}", client.ClientId, group.Key, change.ObjectId, change.Id, change.EntityState, change.CreatedAt);
                        entityDataMap.TryGetValue(change.ObjectId, out var entityData);

                        await clientManager.SendNotificationAsync(
                            group.Key,
                            change.Id,
                            change.ObjectId,
                            change.EntityState.ToString(),
                            change.CreatedAt,
                            entityData);

                        if (maxSent == null || change.Id > maxSent)
                        {
                            maxSent = change.Id;
                        }
                    }
                }

                if (maxSent != null)
                {
                    client.LastChangeLogId = maxSent.Value;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error polling for ChangeLog changes");
        }
    }

    /// <summary>
    /// Get entity data for content notifications using reflection.
    /// </summary>
    private async Task<Dictionary<int, object>> GetEntityData(PgDbContext dbContext, string entityType, List<int> entityIds)
    {
        var result = new Dictionary<int, object>();

        try
        {
            // Find the entity type
            var assembly = typeof(PgDbContext).Assembly; // Use the correct assembly
            var type = assembly.GetTypes().FirstOrDefault(t => t.Name == entityType);

            if (type == null)
            {
                logger.LogWarning("Entity type {EntityType} not found", entityType);
                return result;
            }

            // Get the DbSet property
            var dbSetProperty = typeof(PgDbContext).GetProperties()
                .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                               p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                               p.PropertyType.GetGenericArguments()[0] == type);

            if (dbSetProperty == null)
            {
                logger.LogWarning("DbSet for entity type {EntityType} not found", entityType);
                return result;
            }

            // Get entities using reflection
            var dbSet = dbSetProperty.GetValue(dbContext);
            if (dbSet == null)
            {
                return result;
            }

            // Build query: dbSet.Where(entity => entityIds.Contains(entity.Id))
            var whereMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == "Where" && m.GetParameters().Length == 2)
                .MakeGenericMethod(type);

            var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .First(m => m.Name == "ToListAsync" && m.GetParameters().Length == 2)
                .MakeGenericMethod(type);

            // Create lambda: entity => entityIds.Contains(entity.Id)
            var parameter = System.Linq.Expressions.Expression.Parameter(type, "entity");
            var idProperty = System.Linq.Expressions.Expression.Property(parameter, "Id");
            var containsMethod = typeof(List<int>).GetMethod("Contains")!;
            var containsCall = System.Linq.Expressions.Expression.Call(
                System.Linq.Expressions.Expression.Constant(entityIds),
                containsMethod,
                idProperty);
            var lambda = System.Linq.Expressions.Expression.Lambda(containsCall, parameter);

            // Execute query
            var filteredQuery = whereMethod.Invoke(null, new[] { dbSet, lambda });
            var entitiesTask = (Task)toListAsyncMethod.Invoke(null, new[] { filteredQuery, CancellationToken.None })!;
            await entitiesTask;

            var entities = (System.Collections.IList)entitiesTask.GetType().GetProperty("Result")!.GetValue(entitiesTask)!;

            // Try to map to DTOs first, fallback to raw entities
            var detailsDtoTypeName = $"LeadCMS.DTOs.{entityType}DetailsDto";
            var detailsDtoType = assembly.GetType(detailsDtoTypeName);

            foreach (var entity in entities)
            {
                var idValue = (int)entity.GetType().GetProperty("Id")!.GetValue(entity)!;

                try
                {
                    if (detailsDtoType != null)
                    {
                        var dto = mapper.Map(entity, type, detailsDtoType);
                        result[idValue] = dto;
                    }
                    else
                    {
                        result[idValue] = entity;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error mapping entity {EntityType} with ID {EntityId}, using raw entity", entityType, idValue);
                    result[idValue] = entity;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching entity data for type {EntityType}", entityType);
        }

        return result;
    }

    /// <summary>
    /// Get all entity types that support ChangeLog.
    /// </summary>
    private HashSet<Type> GetSupportedTypes()
    {
        var assembly = typeof(PgDbContext).Assembly; // Use the assembly containing the entities
        var types = assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<SupportsChangeLogAttribute>().Any())
            .ToHashSet();

        logger.LogInformation(
            "Found {Count} entity types supporting change notifications: {Types}",
            types.Count,
            string.Join(", ", types.Select(t => t.Name)));

        return types;
    }

    /// <summary>
    /// Build connection string from configuration.
    /// </summary>
    private string BuildConnectionString()
    {
        var postgres = configuration.GetSection("Postgres");
        var server = postgres["Server"] ?? "localhost";
        var port = postgres["Port"] ?? "5432";
        var username = postgres["UserName"] ?? "postgres";
        var password = postgres["Password"] ?? "postgres";
        var database = postgres["Database"] ?? "LeadCMS";

        return $"Host={server};Port={port};Username={username};Password={password};Database={database}";
    }
}
