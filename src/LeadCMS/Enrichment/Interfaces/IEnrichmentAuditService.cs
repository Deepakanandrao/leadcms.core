// <copyright file="IEnrichmentAuditService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enrichment.Models;

namespace LeadCMS.Enrichment.Interfaces;

public interface IEnrichmentAuditService
{
    Task WriteAuditAsync(string providerKey, string entityType, int entityId, IReadOnlyCollection<EnrichedFieldChange> changes);
}
