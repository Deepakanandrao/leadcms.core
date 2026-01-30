// <copyright file="EnrichmentSchedulerTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Enrichment.Interfaces;
using LeadCMS.Entities;
using LeadCMS.Services;
using LeadCMS.Tasks;

namespace LeadCMS.Enrichment.Tasks;

/// <summary>
/// Reads ChangeLog and creates enrichment work items for enabled providers and supported triggers.
/// </summary>
public class EnrichmentSchedulerTask(
    IConfiguration configuration,
    PgDbContext dbContext,
    IEnumerable<PluginDbContextBase> pluginDbContexts,
    TaskStatusService taskStatusService,
    IEnrichmentProviderResolver providerResolver,
    IEnrichmentWorkItemService workItemService) : ChangeLogTask("Tasks:EnrichmentSchedulerTask", configuration, dbContext, pluginDbContexts, taskStatusService)
{
    private readonly IEnrichmentProviderResolver providerResolver = providerResolver;
    private readonly IEnrichmentWorkItemService workItemService = workItemService;

    protected override bool IsTypeSupported(Type type)
    {
        var typeName = type.Name;
        return providerResolver.All.Any(p => p.SupportedEntityTypes.Contains(typeName, StringComparer.OrdinalIgnoreCase));
    }

    protected override string? ExecuteLogTask(List<ChangeLog> nextBatch, Type loggedType)
    {
        var enqueuedCount = 0;

        foreach (var change in nextBatch)
        {
            var trigger = change.EntityState switch
            {
                Microsoft.EntityFrameworkCore.EntityState.Added => EnrichmentTrigger.Created,
                Microsoft.EntityFrameworkCore.EntityState.Modified => EnrichmentTrigger.Updated,
                _ => (EnrichmentTrigger?)null,
            };

            if (trigger is null)
            {
                continue;
            }

            foreach (var provider in providerResolver.All.Where(p => p.SupportedEntityTypes.Contains(loggedType.Name, StringComparer.OrdinalIgnoreCase) && p.SupportedTriggers.Contains(trigger.Value)))
            {
                workItemService.EnqueueAsync(provider.ProviderKey, loggedType.Name, change.ObjectId, trigger.Value).GetAwaiter().GetResult();
                enqueuedCount++;
            }
        }

        return enqueuedCount > 0 ? $"Enqueued {enqueuedCount} enrichment work items" : null;
    }
}
