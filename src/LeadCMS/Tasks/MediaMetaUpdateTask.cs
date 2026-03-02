// <copyright file="MediaMetaUpdateTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Services;

namespace LeadCMS.Tasks;

/// <summary>
/// Background task that re-indexes media usage metadata whenever content changes
/// are detected. Runs a full scan of all content to update usage counts,
/// descriptions, and content-type tags on media items.
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
        var result = mediaUsageService.UpdateMediaUsageFromAllContentAsync().GetAwaiter().GetResult();

        Log.Information(
            "MediaMetaUpdateTask: Scanned {ContentsProcessed} contents, updated {MediaUpdated} media items",
            result.ContentsProcessed,
            result.MediaUpdated);

        return $"Scanned {result.ContentsProcessed} contents, updated {result.MediaUpdated} media items";
    }

    protected override bool IsTypeSupported(Type type)
    {
        return type == typeof(Content);
    }
}
