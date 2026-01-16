// <copyright file="EnrichmentAuditService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Enrichment.Interfaces;
using LeadCMS.Enrichment.Models;
using LeadCMS.Entities;

namespace LeadCMS.Enrichment.Services;

public class EnrichmentAuditService : IEnrichmentAuditService
{
    private readonly PgDbContext dbContext;

    public EnrichmentAuditService(PgDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task WriteAuditAsync(string providerKey, string entityType, int entityId, IReadOnlyCollection<EnrichedFieldChange> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var rows = changes.Select(c => new EnrichmentAudit
        {
            ProviderKey = providerKey,
            EntityType = entityType,
            EntityId = entityId,
            FieldName = c.FieldName,
            OldValue = c.OldValue,
            NewValue = c.NewValue,
            Confidence = c.Confidence,
            EnrichedAt = DateTime.UtcNow,
        });

        await dbContext.EnrichmentAudits!.AddRangeAsync(rows);
    }
}
