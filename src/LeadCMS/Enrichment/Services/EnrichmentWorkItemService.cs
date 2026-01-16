// <copyright file="EnrichmentWorkItemService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Enrichment.Interfaces;
using LeadCMS.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Enrichment.Services;

public class EnrichmentWorkItemService(PgDbContext dbContext) : IEnrichmentWorkItemService
{
    private readonly PgDbContext dbContext = dbContext;

    public async Task<EnrichmentWorkItem?> EnqueueAsync(string providerKey, string entityType, int entityId, EnrichmentTrigger trigger)
    {
        var pendingOrInProgress = await dbContext.EnrichmentWorkItems!
            .FirstOrDefaultAsync(w => w.ProviderKey == providerKey && w.EntityType == entityType && w.EntityId == entityId && (w.Status == EnrichmentWorkItemStatus.Pending || w.Status == EnrichmentWorkItemStatus.InProgress));

        if (pendingOrInProgress is not null)
        {
            return pendingOrInProgress;
        }

        var workItem = new EnrichmentWorkItem
        {
            ProviderKey = providerKey,
            EntityType = entityType,
            EntityId = entityId,
            Trigger = trigger,
            Status = EnrichmentWorkItemStatus.Pending,
            ScheduledAt = DateTime.UtcNow,
        };

        dbContext.EnrichmentWorkItems!.Add(workItem);
        return workItem;
    }

    public async Task<IReadOnlyCollection<EnrichmentWorkItem>> GetPendingAsync(int take)
    {
        return await dbContext.EnrichmentWorkItems!
            .Where(w => w.Status == EnrichmentWorkItemStatus.Pending)
            .OrderBy(w => w.ScheduledAt)
            .ThenBy(w => w.Id)
            .Take(take)
            .ToListAsync();
    }

    public void MarkInProgress(EnrichmentWorkItem workItem)
    {
        workItem.Status = EnrichmentWorkItemStatus.InProgress;
        workItem.ExecutedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(EnrichmentWorkItem workItem)
    {
        workItem.Status = EnrichmentWorkItemStatus.Completed;
    }

    public void MarkFailed(EnrichmentWorkItem workItem, bool incrementRetry = false)
    {
        workItem.Status = EnrichmentWorkItemStatus.Failed;

        if (incrementRetry)
        {
            workItem.RetryCount += 1;
        }
    }

    public void MarkBlocked(EnrichmentWorkItem workItem)
    {
        workItem.Status = EnrichmentWorkItemStatus.Blocked;
    }
}
