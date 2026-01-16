// <copyright file="EnrichmentProviderResolver.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enrichment.Interfaces;

namespace LeadCMS.Enrichment.Services;

public class EnrichmentProviderResolver : IEnrichmentProviderResolver
{
    private readonly IReadOnlyCollection<IDataEnrichmentProvider> providers;
    private readonly Dictionary<string, IDataEnrichmentProvider> byKey;

    public EnrichmentProviderResolver(IEnumerable<IDataEnrichmentProvider> providers)
    {
        this.providers = providers.ToArray();
        byKey = this.providers.ToDictionary(p => p.ProviderKey, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IDataEnrichmentProvider> All => providers;

    public IDataEnrichmentProvider? Resolve(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return null;
        }

        return byKey.TryGetValue(providerKey, out var provider) ? provider : null;
    }
}
