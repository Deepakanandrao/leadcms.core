// <copyright file="ISettingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

public interface ISettingService
{
    Task<string?> GetUserSettingAsync(string key, string userId);

    Task<string?> GetSystemSettingAsync(string key);

    Task SetUserSettingAsync(string key, string? value, string userId);

    Task SetSystemSettingAsync(string key, string? value);

    Task DeleteUserSettingAsync(string key, string userId);

    Task DeleteSystemSettingAsync(string key);

    Task<Dictionary<string, string?>> GetEffectiveUserSettingsAsync(string userId);

    Task<Dictionary<string, string?>> GetSettingsByKeysAsync(IEnumerable<string> keys, string? userId = null);

    /// <summary>
    /// Gets a setting value with fallback to configuration. Checks database first, then configuration section.
    /// </summary>
    /// <param name="key">Setting key (e.g., "Content.MinTitleLength").</param>
    /// <param name="configurationPath">Configuration path (e.g., "Content:MinTitleLength").</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>Setting value or null if not found.</returns>
    Task<string?> GetSettingWithFallbackAsync(string key, string configurationPath, string? userId = null);

    /// <summary>
    /// Gets an integer setting value with fallback to configuration.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="configurationPath">Configuration path.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>Integer setting value.</returns>
    Task<int> GetIntSettingWithFallbackAsync(string key, string configurationPath, int defaultValue = 0, string? userId = null);

    /// <summary>
    /// Gets an integer setting value with automatic configuration path conversion using convention.
    /// </summary>
    /// <param name="settingKey">Setting key.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>Integer setting value.</returns>
    Task<int> GetIntSettingWithFallbackAsync(string settingKey, int defaultValue = 0, string? userId = null);

    /// <summary>
    /// Gets a boolean setting value with fallback to configuration.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="configurationPath">Configuration path.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>Boolean setting value.</returns>
    Task<bool> GetBoolSettingWithFallbackAsync(string key, string configurationPath, bool defaultValue = false, string? userId = null);

    /// <summary>
    /// Gets a boolean setting value with automatic configuration path conversion using convention.
    /// </summary>
    /// <param name="settingKey">Setting key.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <returns>Boolean setting value.</returns>
    Task<bool> GetBoolSettingWithFallbackAsync(string settingKey, bool defaultValue = false, string? userId = null);
}
