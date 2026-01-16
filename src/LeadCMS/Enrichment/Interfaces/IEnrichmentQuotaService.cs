// <copyright file="IEnrichmentQuotaService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Enrichment.Interfaces;

public interface IEnrichmentQuotaService
{
    Task<bool> TryConsumeAsync(EnrichmentProviderConfig providerConfig);
}
