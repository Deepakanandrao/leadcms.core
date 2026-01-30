// <copyright file="MediaMetaUpdateTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Services;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Tasks;

/// <summary>
/// Background task that updates media usage metadata based on content changes.
/// This task processes the change log for Content entities and updates media
/// usage counts and descriptions accordingly.
/// </summary>
public class MediaMetaUpdateTask : ChangeLogTask
{
    private readonly IMediaUsageService mediaUsageService;

    public MediaMetaUpdateTask(
        IConfiguration configuration,
        PgDbContext dbContext,
        IEnumerable<PluginDbContextBase> pluginDbContexts,
        IMediaUsageService mediaUsageService,
        TaskStatusService taskStatusService)
        : base("Tasks:MediaMetaUpdateTask", configuration, dbContext, pluginDbContexts, taskStatusService)
    {
        this.mediaUsageService = mediaUsageService;
    }

    protected override string? ExecuteLogTask(List<ChangeLog> nextBatch, Type loggedType)
    {
        // Group by content ID and get the latest change log entry for each content
        // (the one with the highest Id, which is the most recent change)
        var latestChangesByContent = nextBatch
            .Where(cl => cl.EntityState != EntityState.Deleted)
            .GroupBy(cl => cl.ObjectId)
            .Select(g => g.OrderByDescending(cl => cl.Id).First())
            .ToList();

        var descriptionsUpdated = 0;

        foreach (var changeLog in latestChangesByContent)
        {
            // Try to extract the Body field from the change log data
            var body = ExtractBodyFromChangeLog(changeLog.Data);
            if (!string.IsNullOrWhiteSpace(body))
            {
                mediaUsageService.UpdateMediaDescriptionsFromContentAsync(body).GetAwaiter().GetResult();
                descriptionsUpdated++;
            }
        }

        // Update usage counts for all media items
        var result = mediaUsageService.UpdateMediaUsageFromAllContentAsync().GetAwaiter().GetResult();

        Log.Information(
            "MediaMetaUpdateTask: Processed {ContentsProcessed} contents, updated {MediaUpdated} media items, updated descriptions from {DescriptionsUpdated} content changes",
            result.ContentsProcessed,
            result.MediaUpdated,
            descriptionsUpdated);

        return $"Scanned {result.ContentsProcessed} contents, updated {result.MediaUpdated} media items, processed descriptions from {descriptionsUpdated} content changes";
    }

    protected override bool IsTypeSupported(Type type)
    {
        // Only process Content entity changes
        return type == typeof(Content);
    }

    /// <summary>
    /// Extracts the Body field from the change log JSON data.
    /// </summary>
    private static string? ExtractBodyFromChangeLog(string jsonData)
    {
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonData);
            if (document.RootElement.TryGetProperty("Body", out var bodyElement) ||
                document.RootElement.TryGetProperty("body", out bodyElement))
            {
                return bodyElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Ignore JSON parsing errors
        }

        return null;
    }
}
