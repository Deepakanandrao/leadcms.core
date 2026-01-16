// <copyright file="IEnrichmentProviderResolver.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enrichment.Interfaces;

/// <summary>
/// Resolves providers by key and exposes the registered set.
/// </summary>
public interface IEnrichmentProviderResolver
{
    IReadOnlyCollection<IDataEnrichmentProvider> All { get; }

    IDataEnrichmentProvider? Resolve(string providerKey);
}
