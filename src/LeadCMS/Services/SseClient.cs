// <copyright file="SseClient.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;

namespace LeadCMS.Services;

/// <summary>
/// Represents an SSE client connection with change tracking.
/// </summary>
public class SseClient
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ClientId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public HttpResponse Response { get; set; } = null!;

    public CancellationToken CancellationToken { get; set; }

    public int LastChangeLogId { get; set; }

    // Add this property to track the last draft update timestamp
    public DateTime? LastDraftUpdateAt { get; set; }

    public bool IncludeLiveDrafts { get; set; } // true if client wants draft updates

    public HashSet<string> SubscribedEntities { get; set; } = new();

    public bool IncludeContent { get; set; }

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Send an SSE message to the client.
    /// </summary>
    public async Task SendMessageAsync(string eventType, object data)
    {
        if (CancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var message = $"event: {eventType}\ndata: {System.Text.Json.JsonSerializer.Serialize(data)}\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            await Response.Body.WriteAsync(bytes, CancellationToken);
            await Response.Body.FlushAsync(CancellationToken);
        }
        catch (Exception)
        {
            // Client disconnected
        }
    }

    /// <summary>
    /// Send a keep-alive ping to maintain connection.
    /// </summary>
    public async Task SendKeepAliveAsync()
    {
        if (CancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var message = ": keep-alive\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            await Response.Body.WriteAsync(bytes, CancellationToken);
            await Response.Body.FlushAsync(CancellationToken);
        }
        catch (Exception)
        {
            // Client disconnected
        }
    }
}
