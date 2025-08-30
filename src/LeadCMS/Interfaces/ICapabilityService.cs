// <copyright file="ICapabilityService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for managing system capabilities.
/// </summary>
public interface ICapabilityService
{
    /// <summary>
    /// Gets all available capabilities from registered providers.
    /// </summary>
    /// <returns>A collection of capability names.</returns>
    IEnumerable<string> GetAllCapabilities();

    /// <summary>
    /// Registers a capability provider.
    /// </summary>
    /// <param name="provider">The capability provider to register.</param>
    void RegisterProvider(ICapabilityProvider provider);
}
