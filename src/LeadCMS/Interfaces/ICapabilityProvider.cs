// <copyright file="ICapabilityProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Interface for plugins to provide capabilities to the system.
/// </summary>
public interface ICapabilityProvider
{
    /// <summary>
    /// Gets the capabilities provided by this provider.
    /// </summary>
    /// <returns>A collection of capability names.</returns>
    IEnumerable<string> GetCapabilities();
}
