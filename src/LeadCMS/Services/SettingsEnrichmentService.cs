// <copyright file="SettingsEnrichmentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Constants;
using LeadCMS.Entities;
using LeadCMS.Interfaces;

namespace LeadCMS.Services;

/// <summary>
/// Service for enriching settings lists with default values from configuration.
/// Handles null database values by falling back to configuration defaults.
/// </summary>
public class SettingsEnrichmentService : ISettingsEnrichmentService
{
    private readonly ISettingService settingService;
    private readonly IConfiguration configuration;
    private readonly IReadOnlyList<SettingDefinition> settingDefinitions;

    public SettingsEnrichmentService(
        ISettingService settingService,
        IConfiguration configuration,
        IEnumerable<ISettingsProvider> settingsProviders)
    {
        this.settingService = settingService;
        this.configuration = configuration;
        settingDefinitions = settingsProviders
            .SelectMany(p => p.GetSettingDefinitions())
            .GroupBy(d => d.Key)
            .Select(g => g.First())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Enriches settings list with content validation defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    public async Task EnrichWithContentValidationSettingsAsync(List<Setting> settings, string? userId = null)
    {
        var minTitleLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MinTitleLength, 10, userId);
        var maxTitleLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MaxTitleLength, 60, userId);
        var minDescriptionLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MinDescriptionLength, 20, userId);
        var maxDescriptionLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.MaxDescriptionLength, 155, userId);
        var enableRealtimeSyntaxValidation = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.EnableRealtimeSyntaxValidation, true, userId);

        SetSettingIfNullOrEmpty(settings, SettingKeys.MinTitleLength, minTitleLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MaxTitleLength, maxTitleLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MinDescriptionLength, minDescriptionLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MaxDescriptionLength, maxDescriptionLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.EnableRealtimeSyntaxValidation, enableRealtimeSyntaxValidation.ToString().ToLower());
    }

    /// <summary>
    /// Enriches settings list with identity/password policy defaults.
    /// Uses SettingService fallback methods to handle null database values.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    public async Task EnrichWithIdentitySettingsAsync(List<Setting> settings, string? userId = null)
    {
        var requireDigit = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireDigit, userId == null, userId);
        var requireUppercase = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireUppercase, userId == null, userId);
        var requireLowercase = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireLowercase, true, userId);
        var requireNonAlphanumeric = await settingService.GetBoolSettingWithFallbackAsync(SettingKeys.RequireNonAlphanumeric, userId == null, userId);
        var requiredLength = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.RequiredLength, 6, userId);
        var requiredUniqueChars = await settingService.GetIntSettingWithFallbackAsync(SettingKeys.RequiredUniqueChars, 1, userId);

        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireDigit, requireDigit.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireUppercase, requireUppercase.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireLowercase, requireLowercase.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequireNonAlphanumeric, requireNonAlphanumeric.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequiredLength, requiredLength.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.RequiredUniqueChars, requiredUniqueChars.ToString());
    }

    /// <summary>
    /// Enriches settings list with API configuration defaults.
    /// These settings are typically configuration-only and don't have database overrides.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    public async Task EnrichWithApiSettingsAsync(List<Setting> settings)
    {
        var maxListSize = configuration["ApiSettings:MaxListSize"] ?? "100";
        var defaultFromEmail = configuration["ApiSettings:DefaultFromEmail"] ?? "no-reply@leadcms.ai";
        var defaultFromName = configuration["ApiSettings:DefaultFromName"] ?? "LeadCMS";

        SetSettingIfNullOrEmpty(settings, "ApiSettings.MaxListSize", maxListSize);
        SetSettingIfNullOrEmpty(settings, "ApiSettings.DefaultFromEmail", defaultFromEmail);
        SetSettingIfNullOrEmpty(settings, "ApiSettings.DefaultFromName", defaultFromName);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Enriches settings list with media optimization defaults.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    public async Task EnrichWithMediaSettingsAsync(List<Setting> settings, string? userId = null)
    {
        var maxDimensions = await settingService.GetSettingWithFallbackAsync(
            SettingKeys.MediaMaxDimensions,
            ConfigurationPaths.GetConfigurationPath(SettingKeys.MediaMaxDimensions),
            userId);
        var coverDimensions = await settingService.GetSettingWithFallbackAsync(
            SettingKeys.MediaCoverDimensions,
            ConfigurationPaths.GetConfigurationPath(SettingKeys.MediaCoverDimensions),
            userId);
        var preferredFormat = await settingService.GetSettingWithFallbackAsync(
            SettingKeys.MediaPreferredFormat,
            ConfigurationPaths.GetConfigurationPath(SettingKeys.MediaPreferredFormat),
            userId);

        var maxFileSizeInKb = GetDefaultMediaMaxFileSize();

        var enableOptimisationConfig = configuration["Media:EnableOptimisation"] ?? "false";
        var enableOptimisation = bool.TryParse(enableOptimisationConfig, out var result) && result;

        SetSettingIfNullOrEmpty(settings, SettingKeys.MediaMaxDimensions, string.IsNullOrWhiteSpace(maxDimensions) ? "1024x1024" : maxDimensions!);
        SetSettingIfNullOrEmpty(settings, SettingKeys.MediaCoverDimensions, string.IsNullOrWhiteSpace(coverDimensions) ? "512x256" : coverDimensions!);
        SetSettingIfNullOrEmpty(settings, SettingKeys.MediaPreferredFormat, string.IsNullOrWhiteSpace(preferredFormat) ? "avif" : preferredFormat!);
        SetSettingIfNullOrEmpty(settings, SettingKeys.MediaMaxFileSize, maxFileSizeInKb.ToString());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MediaEnableOptimisation, enableOptimisation.ToString().ToLower());
        SetSettingIfNullOrEmpty(settings, SettingKeys.MediaQuality, "75");
        SetSettingIfNullOrEmpty(settings, SettingKeys.MediaEnableCoverResize, "false");
    }

    /// <summary>
    /// Enriches settings list with lead capture defaults.
    /// Falls back to ContactUs.To configuration when LeadCapture.Email.Recipients is missing or empty.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    public async Task EnrichWithLeadCaptureSettingsAsync(List<Setting> settings)
    {
        const string leadCaptureEmailRecipientsKey = "LeadCapture.Email.Recipients";

        var existing = settings.FirstOrDefault(s => s.Key == leadCaptureEmailRecipientsKey);
        var hasValue = existing != null
            && !string.IsNullOrWhiteSpace(existing.Value)
            && !string.Equals(existing.Value, "[]", StringComparison.Ordinal);

        if (!hasValue)
        {
            var contactUsTo = configuration.GetSection("ContactUs:To").Get<string[]>();
            if (contactUsTo != null && contactUsTo.Length > 0)
            {
                if (existing != null)
                {
                    existing.Value = JsonSerializer.Serialize(contactUsTo);
                }
                else
                {
                    settings.Add(new Setting { Key = leadCaptureEmailRecipientsKey, Value = JsonSerializer.Serialize(contactUsTo) });
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Enriches settings list with all known settings categories.
    /// This is a convenience method that calls all specific enrichment methods.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    public async Task EnrichWithAllKnownSettingsAsync(List<Setting> settings, string? userId = null)
    {
        await EnrichWithContentValidationSettingsAsync(settings, userId);
        await EnrichWithIdentitySettingsAsync(settings, userId);
        await EnrichWithApiSettingsAsync(settings);
        await EnrichWithMediaSettingsAsync(settings, userId);
        await EnrichWithLeadCaptureSettingsAsync(settings);
        EnrichWithPluginSettings(settings);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SettingDefinition> GetSettingDefinitions()
    {
        return settingDefinitions;
    }

    /// <summary>
    /// Sets a setting value in the list only if the key doesn't exist or the value is null/empty.
    /// If the setting doesn't exist, a new Setting is added to the list.
    /// </summary>
    /// <param name="settings">Settings list to update.</param>
    /// <param name="key">Setting key.</param>
    /// <param name="value">Value to set if key is missing or null/empty.</param>
    private static void SetSettingIfNullOrEmpty(List<Setting> settings, string key, string value)
    {
        var existing = settings.FirstOrDefault(s => s.Key == key);
        if (existing == null)
        {
            settings.Add(new Setting { Key = key, Value = value });
        }
        else if (string.IsNullOrEmpty(existing.Value))
        {
            existing.Value = value;
        }
    }

    /// <summary>
    /// Converts a file size string (e.g., "5MB", "1GB", "512KB") to kilobytes.
    /// If the value is already numeric, treats it as kilobytes.
    /// </summary>
    /// <param name="sizeString">Size string to convert (e.g., "5MB", "1024KB", "512").</param>
    /// <returns>Size in kilobytes as a long.</returns>
    private static long ConvertToKilobytes(string sizeString)
    {
        if (string.IsNullOrWhiteSpace(sizeString))
        {
            return 500; // Default 500KB
        }

        sizeString = sizeString.Trim().ToUpperInvariant();

        // If it's just a number, assume it's already in kilobytes
        if (long.TryParse(sizeString, out var kb))
        {
            return kb;
        }

        // Parse size with unit suffix
        var numberPart = System.Text.RegularExpressions.Regex.Match(sizeString, @"[\d.]+").Value;
        if (!double.TryParse(numberPart, System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            return 500; // Default 500KB
        }

        var unit = sizeString.Substring(numberPart.Length).Trim();

        return unit switch
        {
            "B" => (long)(number / 1024),
            "KB" => (long)number,
            "MB" => (long)(number * 1024),
            "GB" => (long)(number * 1024 * 1024),
            _ => (long)number, // Assume KB if unit is unknown
        };
    }

    /// <summary>
    /// Enriches settings list with plugin-registered setting defaults.
    /// Adds any plugin-declared keys that are missing or null in the list.
    /// </summary>
    /// <param name="settings">List of settings to enrich.</param>
    private void EnrichWithPluginSettings(List<Setting> settings)
    {
        foreach (var definition in settingDefinitions)
        {
            SetSettingIfNullOrEmpty(settings, definition.Key, definition.DefaultValue ?? string.Empty);
        }
    }

    /// <summary>
    /// Gets the default media max file size from configuration.
    /// Reads from Media:MaxSize configuration array, finds "default" extension entry, and converts to kilobytes.
    /// </summary>
    /// <returns>Max file size in kilobytes.</returns>
    private long GetDefaultMediaMaxFileSize()
    {
        var mediaSection = configuration.GetSection("Media");
        var maxSizeConfig = mediaSection.GetSection("MaxSize");

        // Try to find the default entry
        var defaultEntry = maxSizeConfig
            .GetChildren()
            .FirstOrDefault(item => item["Extension"] == "default");

        if (defaultEntry != null && !string.IsNullOrWhiteSpace(defaultEntry["MaxSize"]))
        {
            return ConvertToKilobytes(defaultEntry["MaxSize"]!);
        }

        // Fallback: 500KB default
        return 500;
    }
}