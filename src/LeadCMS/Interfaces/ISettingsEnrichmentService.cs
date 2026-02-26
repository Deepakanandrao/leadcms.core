// <copyright file="ISettingsEnrichmentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for enriching settings lists with default values from configuration.
/// Handles null database values by falling back to configuration defaults.
/// </summary>
public interface ISettingsEnrichmentService
{
    /// <summary>
    /// Enriches settings list with content validation defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithContentValidationSettingsAsync(List<Setting> settings, string? userId = null);

    /// <summary>
    /// Enriches settings list with identity/password policy defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithIdentitySettingsAsync(List<Setting> settings, string? userId = null);

    /// <summary>
    /// Enriches settings list with API configuration defaults.
    /// These settings are typically configuration-only and don't have database overrides.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithApiSettingsAsync(List<Setting> settings);

    /// <summary>
    /// Enriches settings list with media optimization defaults.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithMediaSettingsAsync(List<Setting> settings, string? userId = null);

    /// <summary>
    /// Enriches settings list with lead capture defaults.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithLeadCaptureSettingsAsync(List<Setting> settings);

    /// <summary>
    /// Enriches settings list with all known settings categories.
    /// This is a convenience method that calls all specific enrichment methods.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>A task that represents the asynchronous enrichment operation.</returns>
    Task EnrichWithAllKnownSettingsAsync(List<Setting> settings, string? userId = null);

    /// <summary>
    /// Gets all registered setting definitions merged into a flat collection.
    /// </summary>
    /// <returns>A collection of all setting definitions.</returns>
    IReadOnlyList<SettingDefinition> GetSettingDefinitions();
}