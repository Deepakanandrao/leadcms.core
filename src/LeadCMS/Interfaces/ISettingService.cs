// <copyright file="ISettingService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Interfaces;

public interface ISettingService
{
    /// <summary>
    /// Gets a user-level setting value by key, falling back to system-level.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching
    /// (primary language subtag match, e.g. "ru" matches "ru-RU" and vice versa).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>Setting value or null.</returns>
    Task<string?> GetUserSettingAsync(string key, string userId, string? language = null);

    /// <summary>
    /// Gets a system-level setting value by key.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching
    /// (primary language subtag match, e.g. "ru" matches "ru-RU" and vice versa).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>Setting value or null.</returns>
    Task<string?> GetSystemSettingAsync(string key, string? language = null);

    /// <summary>
    /// Finds a system-level setting by key, falling back to plugin definitions for settings not yet saved in DB.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching
    /// (primary language subtag match, e.g. "ru" matches "ru-RU" and vice versa).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>The setting entity (real or virtual from plugin definition), or null if not found.</returns>
    Task<Setting?> FindSystemSettingAsync(string key, string? language = null);

    /// <summary>
    /// Finds the effective setting for a user by key: user-level first, then system-level fallback.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching
    /// (primary language subtag match, e.g. "ru" matches "ru-RU" and vice versa).
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>The effective setting entity, or null if not found.</returns>
    Task<Setting?> FindEffectiveUserSettingAsync(string key, string userId, string? language = null);

    /// <summary>
    /// Gets all effective settings for a user: system-level as base, with user-level overrides on top.
    /// When a language is provided, language-specific overrides are layered on top of generic settings
    /// using fuzzy matching (primary language subtag match).
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>List of effective settings.</returns>
    Task<List<Setting>> GetEffectiveUserSettingEntitiesAsync(string userId, string? language = null);

    Task SetUserSettingAsync(string key, string? value, string userId);

    Task SetSystemSettingAsync(string key, string? value, string? language = null);

    Task DeleteUserSettingAsync(string key, string userId);

    Task DeleteSystemSettingAsync(string key, string? language = null);

    /// <summary>
    /// Finds settings by a set of keys, with optional user-level overrides.
    /// System-level settings are returned first, then user-level settings override them by key.
    /// When a language is provided, language-specific overrides are layered using fuzzy matching.
    /// </summary>
    /// <param name="keys">The setting keys to find.</param>
    /// <param name="userId">Optional user ID for user-level overrides.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>List of effective settings for the given keys.</returns>
    Task<List<Setting>> FindSettingsByKeysAsync(IEnumerable<string> keys, string? userId = null, string? language = null);

    /// <summary>
    /// Gets a setting value with fallback to configuration. Checks database first, then configuration section.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching.
    /// </summary>
    /// <param name="key">Setting key (e.g., "Content.MinTitleLength").</param>
    /// <param name="configurationPath">Configuration path (e.g., "Content:MinTitleLength").</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>Setting value or null if not found.</returns>
    Task<string?> GetSettingWithFallbackAsync(string key, string configurationPath, string? userId = null, string? language = null);

    /// <summary>
    /// Gets an integer setting value with fallback to configuration.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="configurationPath">Configuration path.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>Integer setting value.</returns>
    Task<int> GetIntSettingWithFallbackAsync(string key, string configurationPath, int defaultValue = 0, string? userId = null, string? language = null);

    /// <summary>
    /// Gets an integer setting value with automatic configuration path conversion using convention.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching.
    /// </summary>
    /// <param name="settingKey">Setting key.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>Integer setting value.</returns>
    Task<int> GetIntSettingWithFallbackAsync(string settingKey, int defaultValue = 0, string? userId = null, string? language = null);

    /// <summary>
    /// Gets a boolean setting value with fallback to configuration.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching.
    /// </summary>
    /// <param name="key">Setting key.</param>
    /// <param name="configurationPath">Configuration path.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>Boolean setting value.</returns>
    Task<bool> GetBoolSettingWithFallbackAsync(string key, string configurationPath, bool defaultValue = false, string? userId = null, string? language = null);

    /// <summary>
    /// Gets a boolean setting value with automatic configuration path conversion using convention.
    /// When a language is provided, language-specific overrides are resolved using fuzzy matching.
    /// </summary>
    /// <param name="settingKey">Setting key.</param>
    /// <param name="defaultValue">Default value if setting is not found or invalid.</param>
    /// <param name="userId">Optional user ID for user-level settings.</param>
    /// <param name="language">Optional language code for language-specific override resolution.</param>
    /// <returns>Boolean setting value.</returns>
    Task<bool> GetBoolSettingWithFallbackAsync(string settingKey, bool defaultValue = false, string? userId = null, string? language = null);
}
