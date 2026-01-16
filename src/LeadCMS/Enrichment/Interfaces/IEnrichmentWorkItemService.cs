// <copyright file="IEnrichmentWorkItemService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Enrichment.Interfaces;

public interface IEnrichmentWorkItemService
{
    Task<EnrichmentWorkItem?> EnqueueAsync(string providerKey, string entityType, int entityId, EnrichmentTrigger trigger);

    Task<IReadOnlyCollection<EnrichmentWorkItem>> GetPendingAsync(int take);

    void MarkInProgress(EnrichmentWorkItem workItem);

    void MarkCompleted(EnrichmentWorkItem workItem);

    void MarkFailed(EnrichmentWorkItem workItem, bool incrementRetry = false);

    void MarkBlocked(EnrichmentWorkItem workItem);
}
