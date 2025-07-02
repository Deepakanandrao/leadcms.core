// <copyright file="SseController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using LeadCMS.Data;
using LeadCMS.DataAnnotations;
using LeadCMS.Interfaces;
using LeadCMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

/// <summary>
/// Controller for Server-Sent Events (SSE) change notifications.
/// </summary>
[Authorize]
[Route("api/[controller]")]
public class SseController : ControllerBase
{
    private readonly SseClientManager clientManager;
    private readonly PgDbContext dbContext;
    private readonly ILogger<SseController> logger;
    private readonly IHttpContextHelper httpContextHelper;

    public SseController(
        SseClientManager clientManager,
        PgDbContext dbContext,
        ILogger<SseController> logger,
        IHttpContextHelper httpContextHelper)
    {
        this.clientManager = clientManager;
        this.dbContext = dbContext;
        this.logger = logger;
        this.httpContextHelper = httpContextHelper;
    }

    /// <summary>
    /// Get list of supported entity types for change notifications.
    /// </summary>
    /// <returns>Array of entity type names that support change notifications.</returns>
    [HttpGet("supported-entities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<string[]> GetSupportedEntities()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var supportedTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<SupportsChangeLogAttribute>().Any())
            .Select(t => t.Name)
            .OrderBy(name => name)
            .ToArray();

        return Ok(supportedTypes);
    }

    /// <summary>
    /// Get connection information for SSE endpoint.
    /// </summary>
    /// <returns>Connection information including endpoint URL and parameters.</returns>
    [HttpGet("connection-info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<object> GetConnectionInfo()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        
        return Ok(new
        {
            EndpointUrl = $"{baseUrl}/api/sse/stream",
            Method = "GET",
            Parameters = new
            {
                entities = "Array of entity types to subscribe to (e.g., 'Contact,Deal') or '*' for all",
                includeContent = "Boolean: true for full content, false for notifications only (default: false)",
                includeLiveDrafts = "Boolean: true to include live draft updates, false to exclude (default: false)",
            },
            Example = $"{baseUrl}/api/sse/stream?entities=Contact,Deal&includeContent=true,includeLiveDrafts=true",
            Documentation = new
            {
                Description = "Server-Sent Events endpoint for real-time change notifications",
                Notes = new[]
                {
                    "Client receives changes that occur after connection is established",
                    "Each client tracks its own position in the ChangeLog",
                    "Use includeContent=true to receive full entity data",
                    "Use includeContent=false for lightweight notifications only",
                },
            },
        });
    }

    /// <summary>
    /// SSE stream endpoint for real-time change notifications.
    /// </summary>
    /// <param name="entities">Comma-separated list of entity types to subscribe to, or '*' for all.</param>
    /// <param name="includeContent">Whether to include full entity content in notifications.</param>
    /// <param name="includeLiveDrafts">Whether to include live draft updates in notifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Server-Sent Events stream.</returns>
    [HttpGet("stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StreamChanges(
        [FromQuery] string entities = "*",
        [FromQuery] bool includeContent = false,
        [FromQuery] bool includeLiveDrafts = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate and parse entities parameter
            var subscribedEntities = ParseEntitiesParameter(entities);

            if (subscribedEntities == null)
            {
                logger.LogInformation("[SSE] Invalid entities parameter received: {Entities}", entities);
                return BadRequest(new { error = "Invalid entities parameter. Use comma-separated entity names or '*' for all." });
            }

            // Generate unique client ID
            var clientId = Guid.NewGuid().ToString();

            // Get current max ChangeLog ID as starting point for this client
            var maxChangeLogId = await dbContext.ChangeLogs!.MaxAsync(cl => (int?)cl.Id, cancellationToken) ?? 0;

            // Set up SSE response headers
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["Access-Control-Allow-Origin"] = "*";

            logger.LogInformation("[SSE] New SSE client connection: clientId={ClientId}, entities={Entities}, includeContent={IncludeContent}, includeLiveDrafts={IncludeLiveDrafts}, startingChangeLogId={StartingChangeLogId}", clientId, string.Join(",", subscribedEntities), includeContent, includeLiveDrafts, maxChangeLogId);

            // Send initial connection event
            await WriteSSEEvent("connected", new
            {
                clientId,
                subscribedEntities,
                includeContent,
                startingChangeLogId = maxChangeLogId,
                serverTime = DateTime.UtcNow.ToString("O"),
            });

            // Get the current user ID (assuming claims-based identity)
            var currentUserId = await httpContextHelper.GetCurrentUserIdAsync();

            if (string.IsNullOrEmpty(currentUserId))
            {
                logger.LogInformation("[SSE] Unauthorized SSE connection attempt: clientId={ClientId}", clientId);
                return Unauthorized();
            }

            // Register client with manager
            clientManager.AddClient(clientId, Response, subscribedEntities, includeContent, maxChangeLogId, includeLiveDrafts, currentUserId, cancellationToken);

            logger.LogInformation(
                "SSE client {ClientId} connected for entities: {Entities}, content: {IncludeContent}, drafts: {IncludeLiveDrafts}",
                clientId,
                string.Join(",", subscribedEntities),
                includeContent,
                includeLiveDrafts);

            try
            {
                // Keep connection alive
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Send periodic heartbeat (every 30 seconds)
                    await Task.Delay(30000, cancellationToken);
                    logger.LogInformation("[SSE] Heartbeat sent to client {ClientId}", clientId);
                    await WriteSSEEvent("heartbeat", new { timestamp = DateTime.UtcNow.ToString("O") });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when client disconnects
                logger.LogInformation("[SSE] SSE client {ClientId} connection cancelled", clientId);
            }
            finally
            {
                // Clean up client
                clientManager.RemoveClient(clientId);
                logger.LogInformation("SSE client {ClientId} disconnected", clientId);
            }

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SSE stream");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get current SSE connection statistics.
    /// </summary>
    /// <returns>Connection statistics.</returns>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<object> GetStats()
    {
        return Ok(new
        {
            connectedClients = clientManager.ConnectedClientCount,
            clientIds = clientManager.GetConnectedClientIds(),
        });
    }

    /// <summary>
    /// Parse the entities parameter into an array.
    /// </summary>
    private string[] ParseEntitiesParameter(string entities)
    {
        if (string.IsNullOrWhiteSpace(entities))
        {
            return new[] { "*" };
        }

        if (entities.Trim() == "*")
        {
            return new[] { "*" };
        }

        var entityArray = entities.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToArray();

        // Validate entity names against supported types
        var supportedTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttributes<SupportsChangeLogAttribute>().Any())
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidEntities = entityArray.Where(e => e != "*" && !supportedTypes.Contains(e)).ToArray();
        if (invalidEntities.Any())
        {
            logger.LogWarning("Invalid entity types requested: {InvalidEntities}", string.Join(", ", invalidEntities));
            return null!; // Invalid entities
        }

        return entityArray;
    }

    /// <summary>
    /// Write an SSE event to the response stream.
    /// </summary>
    private async Task WriteSSEEvent(string eventType, object data)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            });
            
            var sseData = $"event: {eventType}\ndata: {json}\n\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(sseData);
            
            await Response.Body.WriteAsync(bytes);
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing SSE event");
        }
    }
}
