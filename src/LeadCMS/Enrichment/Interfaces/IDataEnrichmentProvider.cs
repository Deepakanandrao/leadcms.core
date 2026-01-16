// <copyright file="IDataEnrichmentProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enrichment.Models;
using LeadCMS.Entities;

namespace LeadCMS.Enrichment.Interfaces;

/// <summary>
/// Contract for enrichment providers implemented by plugins.
/// Providers mutate the passed entity instance and return execution metadata.
/// </summary>
public interface IDataEnrichmentProvider
{
    string ProviderKey { get; }

    IReadOnlyCollection<string> SupportedEntityTypes { get; }

    IReadOnlyCollection<EnrichmentTrigger> SupportedTriggers { get; }

    Task<bool> ShouldEnrichAsync(object entity);

    Task<EnrichmentExecutionResult> EnrichAsync(object entity);
}
