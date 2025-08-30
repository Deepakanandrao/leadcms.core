// <copyright file="CapabilityService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Interfaces;

namespace LeadCMS.Services;

/// <summary>
/// Service for managing system capabilities.
/// </summary>
public class CapabilityService : ICapabilityService
{
    private readonly List<ICapabilityProvider> providers = new List<ICapabilityProvider>();
    private readonly IServiceProvider serviceProvider;

    public CapabilityService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        InitializeProviders();
    }

    /// <summary>
    /// Gets all available capabilities from registered providers.
    /// </summary>
    /// <returns>A collection of capability names.</returns>
    public IEnumerable<string> GetAllCapabilities()
    {
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            foreach (var capability in provider.GetCapabilities())
            {
                capabilities.Add(capability);
            }
        }

        return capabilities.OrderBy(c => c);
    }

    /// <summary>
    /// Registers a capability provider.
    /// </summary>
    /// <param name="provider">The capability provider to register.</param>
    public void RegisterProvider(ICapabilityProvider provider)
    {
        if (provider != null && !providers.Contains(provider))
        {
            providers.Add(provider);
        }
    }

    private void InitializeProviders()
    {
        // Get all registered ICapabilityProvider instances from DI
        var capabilityProviders = serviceProvider.GetServices<ICapabilityProvider>();
        foreach (var provider in capabilityProviders)
        {
            RegisterProvider(provider);
        }
    }
}
