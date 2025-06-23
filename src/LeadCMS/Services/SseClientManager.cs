// <copyright file="SseClientManager.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace LeadCMS.Services;

/// <summary>
/// Manages SSE client connections and their tracking state.
/// </summary>
public class SseClientManager
{
    private readonly ConcurrentDictionary<string, SseClient> clients = new();
    private readonly ILogger<SseClientManager> logger;

    public SseClientManager(ILogger<SseClientManager> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Gets count of connected SSE clients.
    /// </summary>
    public int ConnectedClientCount => clients.Count;

    /// <summary>
    /// Add a new SSE client.
    /// </summary>
    /// <param name="clientId">Unique client identifier.</param>
    /// <param name="response">HTTP response stream.</param>
    /// <param name="subscribedEntities">Array of entity types to subscribe to.</param>
    /// <param name="includeContent">Whether to include full entity content.</param>
    /// <param name="lastChangeLogId">Starting ChangeLog ID for this client.</param>
    /// <param name="includeLiveDrafts">Whether to subscribe to draft updates.</param>
    /// <param name="userId">User ID associated with this client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public void AddClient(
        string clientId,
        HttpResponse response,
        string[] subscribedEntities,
        bool includeContent,
        int lastChangeLogId,
        bool includeLiveDrafts,
        string userId,
        CancellationToken cancellationToken)
    {
        var client = new SseClient
        {
            ClientId = clientId,
            Response = response,
            SubscribedEntities = subscribedEntities.ToHashSet(StringComparer.OrdinalIgnoreCase),
            IncludeContent = includeContent,
            LastChangeLogId = lastChangeLogId,
            LastDraftUpdateAt = includeLiveDrafts ? DateTime.UtcNow : null,
            CancellationToken = cancellationToken,
            ConnectedAt = DateTime.UtcNow,
            IncludeLiveDrafts = includeLiveDrafts,
            UserId = userId,
        };

        clients.TryAdd(clientId, client);

        logger.LogInformation(
            "SSE client {ClientId} connected. Subscribed to: {Entities}, IncludeContent: {IncludeContent}, StartingId: {StartingId}, SubscribeDrafts: {SubscribeDrafts}",
            clientId,
            string.Join(", ", subscribedEntities),
            includeContent,
            lastChangeLogId,
            includeLiveDrafts);
    }

    /// <summary>
    /// Remove an SSE client.
    /// </summary>
    /// <param name="clientId">Client identifier to remove.</param>
    public void RemoveClient(string clientId)
    {
        if (clients.TryRemove(clientId, out var client))
        {
            logger.LogInformation(
                "SSE client {ClientId} disconnected after {Duration}",
                clientId,
                DateTime.UtcNow - client.ConnectedAt);
        }
    }

    /// <summary>
    /// Send change notification to relevant clients.
    /// </summary>
    /// <param name="entityType">Type of entity that changed.</param>
    /// <param name="changeLogId">ChangeLog ID.</param>
    /// <param name="entityId">Entity ID.</param>
    /// <param name="operation">Operation performed.</param>
    /// <param name="timestamp">When the change occurred.</param>
    /// <param name="entityData">Full entity data (for content subscribers).</param>
    public async Task SendNotificationAsync(string entityType, int changeLogId, int entityId, string operation, DateTime timestamp, object? entityData = null)
    {
        var tasks = new List<Task>();

        foreach (var client in clients.Values.ToList())
        {
            try
            {
                // Skip if client is not interested in this entity type
                if (!client.SubscribedEntities.Contains(entityType) && !client.SubscribedEntities.Contains("*"))
                {
                    continue;
                }

                // Skip if this change is older than client's last seen change
                if (changeLogId <= client.LastChangeLogId)
                {
                    continue;
                }

                // Create notification object
                var notification = new
                {
                    entityType,
                    entityId,
                    operation,
                    timestamp = timestamp.ToString("O"),
                    changeLogId,
                    data = client.IncludeContent ? entityData : null,
                };

                var task = SendToClientAsync(client, notification);
                tasks.Add(task);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error preparing notification for client {ClientId}", client.ClientId);
            }
        }

        if (tasks.Any())
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Send draft change notification to a specific client.
    /// </summary>
    public async Task SendDraftNotificationAsync(
        SseClient client,
        string objectType,
        int objectId,
        DateTime timestamp,
        object draftData)
    {
        try
        {
            // Only send if client is interested in this object type
            if (!client.IncludeLiveDrafts)
            {
                return;
            }

            if (!client.SubscribedEntities.Contains(objectType) && !client.SubscribedEntities.Contains("*"))
            {
                return;
            }                

            var notification = new
            {
                entityType = objectType,
                entityId = objectId,
                operation = "draft",
                timestamp = timestamp.ToString("O"),
                data = draftData,
            };

            await SendToClientAsync(client, notification); // 0 for ChangeLogId, not used for drafts
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending draft notification to client {ClientId}", client.ClientId);
        }
    }

    /// <summary>
    /// Get all connected client IDs for debugging.
    /// </summary>
    public string[] GetConnectedClientIds()
    {
        return clients.Keys.ToArray();
    }

    /// <summary>
    /// Get the minimum LastChangeLogId from all connected clients.
    /// This is used to determine the starting point for polling.
    /// </summary>
    /// <returns>Minimum LastChangeLogId, or null if no clients connected.</returns>
    public int? GetMinimumLastChangeLogId()
    {
        if (!clients.Any())
        {
            return null;
        }

        return clients.Values.Min(c => c.LastChangeLogId);
    }

    /// <summary>
    /// Returns all connected SSE clients.
    /// </summary>
    public IEnumerable<SseClient> GetClients()
    {
        return clients.Values;
    }

    /// <summary>
    /// Send notification to a specific client.
    /// </summary>
    private async Task SendToClientAsync(SseClient client, object notification)
    {
        try
        {
            if (client.CancellationToken.IsCancellationRequested)
            {
                RemoveClient(client.ClientId);
                return;
            }

            var json = JsonSerializer.Serialize(notification, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var data = $"data: {json}\n\n";
            var bytes = Encoding.UTF8.GetBytes(data);

            await client.Response.Body.WriteAsync(bytes, client.CancellationToken);
            await client.Response.Body.FlushAsync(client.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
            RemoveClient(client.ClientId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending notification to client {ClientId}", client.ClientId);
            RemoveClient(client.ClientId);
        }
    }
}
