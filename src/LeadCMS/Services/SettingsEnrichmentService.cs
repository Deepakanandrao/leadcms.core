// <copyright file="SettingsEnrichmentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using LeadCMS.Interfaces;

namespace LeadCMS.Services;

/// <summary>
/// Service for enriching settings dictionaries with default values from configuration.
/// Handles null database values by falling back to configuration defaults.
/// </summary>
public class SettingsEnrichmentService : ISettingsEnrichmentService
{
    private readonly ISettingService settingService;
    private readonly IConfiguration configuration;

    public SettingsEnrichmentService(ISettingService settingService, IConfiguration configuration)
    {
        this.settingService = settingService;
        this.configuration = configuration;
    }

    /// <summary>
    /// Enriches settings dictionary with content validation defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    public async Task EnrichWithContentValidationSettingsAsync(Dictionary<string, string?> settings, string? userId = null)
    {
        // Get content validation settings with fallback using existing service methods
        var minTitleLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MinTitleLength, 10, userId);
        var maxTitleLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MaxTitleLength, 60, userId);
        var minDescriptionLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MinDescriptionLength, 20, userId);
        var maxDescriptionLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MaxDescriptionLength, 155, userId);

        // Update settings dictionary with fallback values where needed (handles null values)
        SetSettingIfNullOrEmpty(settings, SettingKeys.MinTitleLength, minTitleLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MaxTitleLength, maxTitleLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MinDescriptionLength, minDescriptionLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MaxDescriptionLength, maxDescriptionLength.ToString());
    }

    /// <summary>
    /// Enriches settings dictionary with identity/password policy defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    public async Task EnrichWithIdentitySettingsAsync(Dictionary<string, string?> settings, string? userId = null)
    {
        // Get identity settings with fallback using existing service methods
        // Note: Using different defaults for system vs user level as seen in ConfigController
        var requireDigit = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireDigit, userId == null, userId);
        var requireUppercase = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireUppercase, userId == null, userId);
        var requireLowercase = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireLowercase, true, userId);
        var requireNonAlphanumeric = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireNonAlphanumeric, userId == null, userId);
        var requiredLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.RequiredLength, 6, userId);
        var requiredUniqueChars = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.RequiredUniqueChars, 1, userId);

        // Update settings dictionary with fallback values where needed (handles null values)
        // Use lowercase for boolean values to match ConfigController pattern
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireDigit, requireDigit.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireUppercase, requireUppercase.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireLowercase, requireLowercase.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireNonAlphanumeric, requireNonAlphanumeric.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequiredLength, requiredLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequiredUniqueChars, requiredUniqueChars.ToString());
    }

    /// <summary>
    /// Enriches settings dictionary with API configuration defaults.
    /// These settings are typically configuration-only and don't have database overrides.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    public async Task EnrichWithApiSettingsAsync(Dictionary<string, string?> settings)
    {
        // Get API settings directly from configuration since these don't typically have database overrides
        var defaultLanguage = configuration["ApiSettings:DefaultLanguage"] ?? "en";
        var maxListSize = configuration["ApiSettings:MaxListSize"] ?? "100";
        var defaultFromEmail = configuration["ApiSettings:DefaultFromEmail"] ?? "no-reply@leadcms.ai";
        var defaultFromName = configuration["ApiSettings:DefaultFromName"] ?? "LeadCMS";

        // Update settings dictionary with fallback values where needed (handles null values)
        SetSettingIfNullOrEmpty(settings, "ApiSettings.DefaultLanguage", defaultLanguage);
        SetSettingIfNullOrEmpty(settings, "ApiSettings.MaxListSize", maxListSize);
        SetSettingIfNullOrEmpty(settings, "ApiSettings.DefaultFromEmail", defaultFromEmail);
        SetSettingIfNullOrEmpty(settings, "ApiSettings.DefaultFromName", defaultFromName);

        await Task.CompletedTask; // Make async for consistency
    }

    /// <summary>
    /// Enriches settings dictionary with all known settings categories.
    /// This is a convenience method that calls all specific enrichment methods.
    /// </summary>
    /// <param name="settings">Dictionary of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    public async Task EnrichWithAllKnownSettingsAsync(Dictionary<string, string?> settings, string? userId = null)
    {
        await EnrichWithContentValidationSettingsAsync(settings, userId);
        await EnrichWithIdentitySettingsAsync(settings, userId);
        await EnrichWithApiSettingsAsync(settings);
    }

    /// <summary>
    /// Sets a setting value in the dictionary only if the key doesn't exist or the value is null/empty.
    /// This method handles the null value checking pattern used throughout the codebase.
    /// </summary>
    /// <param name="settings">Settings dictionary to update.</param>
    /// <param name="key">Setting key.</param>
    /// <param name="value">Value to set if key is missing or null/empty.</param>
    private static void SetSettingIfNullOrEmpty(Dictionary<string, string?> settings, string key, string value)
    {
        // Check if key doesn't exist OR if the value is null OR if the value is empty string
        if (!settings.ContainsKey(key) || settings[key] == null || string.IsNullOrEmpty(settings[key]))
        {
            settings[key] = value;
        }
    }
}