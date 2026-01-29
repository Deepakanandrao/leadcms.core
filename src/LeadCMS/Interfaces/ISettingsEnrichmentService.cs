// <copyright file="ISettingsEnrichmentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for enriching settings dictionaries with default values from configuration.
/// Handles null database values by falling back to configuration defaults.
/// </summary>
public interface ISettingsEnrichmentService
{
    /// <summary>
    /// Enriches settings dictionary with content validation defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithContentValidationSettingsAsync(Dictionary<string, string?> settings, string? userId = null);

    /// <summary>
    /// Enriches settings dictionary with identity/password policy defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithIdentitySettingsAsync(Dictionary<string, string?> settings, string? userId = null);

    /// <summary>
    /// Enriches settings dictionary with API configuration defaults.
    /// These settings are typically configuration-only and don't have database overrides.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithApiSettingsAsync(Dictionary<string, string?> settings);

    /// <summary>
    /// Enriches settings dictionary with media optimization defaults.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithMediaSettingsAsync(Dictionary<string, string?> settings, string? userId = null);

    /// <summary>
    /// Enriches settings dictionary with all known settings categories.
    /// This is a convenience method that calls all specific enrichment methods.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithAllKnownSettingsAsync(Dictionary<string, string?> settings, string? userId = null);
}