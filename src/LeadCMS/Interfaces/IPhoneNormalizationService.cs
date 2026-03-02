// <copyright file="IPhoneNormalizationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Geography;

namespace LeadCMS.Interfaces;

/// <summary>
/// Normalizes raw phone input to E.164 format using a multi-strategy approach.
/// </summary>
public interface IPhoneNormalizationService
{
    /// <summary>
    /// Attempts to normalize a raw phone string to E.164 format.
    /// Returns null when the input cannot be parsed by any strategy.
    /// </summary>
    /// <param name="rawPhone">The raw phone input to normalize.</param>
    /// <param name="countryCode">Optional country code from the contact record.</param>
    /// <param name="language">Optional language/locale hint (e.g. "en-US").</param>
    /// <returns>An E.164 formatted phone string, or null if normalization fails.</returns>
    string? Normalize(string? rawPhone, Country? countryCode = null, string? language = null);
}
