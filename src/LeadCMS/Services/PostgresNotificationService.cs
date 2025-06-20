// <copyright file="PostgresNotificationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
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
    private CancellationTokenSource? cancellationTokenSource;
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
        StopAsync(CancellationToken.None).Wait();
        cancellationTokenSource?.Dispose();
        notificationConnection?.Dispose();
        pollingTimer?.Dispose();
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

            // Setup PostgreSQL NOTIFY listener
            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
                                   BuildConnectionString();
            
            notificationConnection = new NpgsqlConnection(connectionString);
            await notificationConnection.OpenAsync(cancellationTokenSource!.Token);
            
            notificationConnection.Notification += OnNotificationReceived;
            
            using var cmd = new NpgsqlCommand("LISTEN entity_changes", notificationConnection);
            await cmd.ExecuteNonQueryAsync(cancellationTokenSource.Token);
            
            logger.LogInformation("Successfully executed LISTEN entity_changes command");

            // Start background listening task
            listeningTask = Task.Run(ListenForNotifications, cancellationTokenSource.Token);

            // Start polling timer as Plan B (every 5 seconds)
            pollingTimer = new Timer(async _ => await PollForChanges(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

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

            // Stop polling timer
            pollingTimer?.Dispose();
            pollingTimer = null;

            // Stop listening task
            if (notificationConnection != null)
            {
                try
                {
                    using var cmd = new NpgsqlCommand("UNLISTEN entity_changes", notificationConnection);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error sending UNLISTEN command");
                }

                notificationConnection.Notification -= OnNotificationReceived;
                await notificationConnection.CloseAsync();
                notificationConnection.Dispose();
                notificationConnection = null;
            }

            if (listeningTask != null)
            {
                await listeningTask;
                listeningTask = null;
            }

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
    private async Task ListenForNotifications()
    {
        try
        {
            while (isListening && !cancellationTokenSource!.Token.IsCancellationRequested)
            {
                if (notificationConnection != null)
                {
                    await notificationConnection.WaitAsync(cancellationTokenSource.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in PostgreSQL notification listening");
        }
    }

    /// <summary>
    /// Handle PostgreSQL NOTIFY events.
    /// </summary>
    private void OnNotificationReceived(object sender, NpgsqlNotificationEventArgs e)
    {
        logger.LogInformation("Received PostgreSQL notification on channel '{Channel}' with payload '{Payload}'", e.Channel, e.Payload);
        
        if (e.Channel == "entity_changes")
        {
            logger.LogInformation("Received PostgreSQL NOTIFY for entity_changes, triggering poll");
            
            // Process changes immediately
            _ = Task.Run(async () => await PollForChanges());
        }
    }

    /// <summary>
    /// Poll for new ChangeLog entries (Plan B and NOTIFY handler).
    /// </summary>
    private async Task PollForChanges()
    {
        if (!isListening || clientManager.ConnectedClientCount == 0)
        {
            logger.LogDebug("Skipping polling: isListening={IsListening}, clientCount={ClientCount}", isListening, clientManager.ConnectedClientCount);
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

            // Get the minimum lastChangeLogId from all clients to determine what to fetch
            var minClientLastId = clientManager.GetMinimumLastChangeLogId();
            if (!minClientLastId.HasValue)
            {
                logger.LogDebug("No minimum client last ID found, skipping polling");
                return; // No clients connected
            }

            logger.LogDebug("Polling for changes since ID {MinClientLastId}", minClientLastId.Value);

            // Get supported type names for database query
            var supportedTypeNames = supportedTypes.Select(t => t.Name).ToList();

            // Get new ChangeLog entries since the minimum client ID
            var newChanges = await dbContext.ChangeLogs!
                .Where(cl => cl.Id > minClientLastId.Value && 
                           supportedTypeNames.Contains(cl.ObjectType))
                .OrderBy(cl => cl.Id)
                .Take(500) // Process in batches
                .ToListAsync();

            if (newChanges.Any())
            {
                logger.LogInformation("Found {Count} new ChangeLog entries for notification", newChanges.Count);

                // Group by entity type for efficient data fetching
                var grouped = newChanges.GroupBy(cl => cl.ObjectType).ToList();

                foreach (var group in grouped)
                {
                    logger.LogDebug("Processing {Count} changes for entity type {EntityType}", group.Count(), group.Key);
                    await ProcessEntityChanges(dbContext, group.Key, group.ToList());
                }

                // Update last polled ID to highest processed
                lastPolledChangeLogId = newChanges.Max(cl => cl.Id);
            }
            else
            {
                logger.LogDebug("No new ChangeLog entries found");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error polling for ChangeLog changes");
        }
    }

    /// <summary>
    /// Process changes for a specific entity type.
    /// </summary>
    private async Task ProcessEntityChanges(PgDbContext dbContext, string entityType, List<ChangeLog> changes)
    {
        try
        {
            logger.LogDebug("Processing {Count} changes for entity type {EntityType}", changes.Count, entityType);
            
            // Get entity data for content subscribers (skip deleted entities)
            var entityDataMap = new Dictionary<int, object>();
            var nonDeletedChanges = changes.Where(cl => cl.EntityState != EntityState.Deleted).ToList();
            
            if (nonDeletedChanges.Any())
            {
                var entityIds = nonDeletedChanges.Select(cl => cl.ObjectId).Distinct().ToList();
                entityDataMap = await GetEntityData(dbContext, entityType, entityIds);
                logger.LogDebug("Retrieved entity data for {Count} {EntityType} entities", entityDataMap.Count, entityType);
            }

            // Send notifications for each change
            foreach (var change in changes)
            {
                entityDataMap.TryGetValue(change.ObjectId, out var entityData);

                logger.LogDebug(
                    "Sending notification for {EntityType} ID {EntityId} (ChangeLog ID {ChangeLogId})", 
                    entityType,
                    change.ObjectId,
                    change.Id);

                await clientManager.SendNotificationAsync(
                    entityType,
                    change.Id,
                    change.ObjectId,
                    change.EntityState.ToString(),
                    change.CreatedAt,
                    entityData);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing changes for entity type {EntityType}", entityType);
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
