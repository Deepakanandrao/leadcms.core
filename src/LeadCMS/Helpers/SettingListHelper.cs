// <copyright file="SettingListHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Entities;

namespace LeadCMS.Helpers;

/// <summary>
/// Helper methods for reading typed values from an in-memory list of <see cref="Setting"/> entities.
/// </summary>
public static class SettingListHelper
{
    /// <summary>
    /// Gets a string setting value by key.
    /// </summary>
    /// <param name="settings">The list of settings to search.</param>
    /// <param name="key">The setting key.</param>
    /// <returns>The setting value, or null if not found or empty.</returns>
    public static string? GetString(List<Setting> settings, string key)
    {
        var setting = settings.FirstOrDefault(s => s.Key == key);
        if (setting != null && !string.IsNullOrEmpty(setting.Value))
        {
            return setting.Value;
        }

        return null;
    }

    /// <summary>
    /// Gets a boolean setting value by key.
    /// </summary>
    /// <param name="settings">The list of settings to search.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The default value when the setting is missing or not parseable.</param>
    /// <returns>The parsed boolean value, or <paramref name="defaultValue"/>.</returns>
    public static bool GetBool(List<Setting> settings, string key, bool defaultValue = false)
    {
        var setting = settings.FirstOrDefault(s => s.Key == key);
        if (setting != null && !string.IsNullOrEmpty(setting.Value))
        {
            return bool.TryParse(setting.Value, out var result) ? result : defaultValue;
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets an integer setting value by key.
    /// </summary>
    /// <param name="settings">The list of settings to search.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The default value when the setting is missing or not parseable.</param>
    /// <returns>The parsed integer value, or <paramref name="defaultValue"/>.</returns>
    public static int GetInt(List<Setting> settings, string key, int defaultValue = 0)
    {
        var setting = settings.FirstOrDefault(s => s.Key == key);
        if (setting != null && !string.IsNullOrEmpty(setting.Value) && int.TryParse(setting.Value, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets a string array setting value by key, supporting JSON arrays and comma-separated values.
    /// </summary>
    /// <param name="settings">The list of settings to search.</param>
    /// <param name="key">The setting key.</param>
    /// <returns>The parsed string array, or an empty array if not found.</returns>
    public static string[] GetStringArray(List<Setting> settings, string key)
    {
        var setting = settings.FirstOrDefault(s => s.Key == key);
        if (setting != null && !string.IsNullOrEmpty(setting.Value))
        {
            try
            {
                var items = JsonSerializer.Deserialize<string[]>(setting.Value);
                if (items != null && items.Length > 0)
                {
                    return items.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray();
                }
            }
            catch (JsonException)
            {
                return setting.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Picks the best language match from a list of settings that share the same key.
    /// Priority order (highest wins):
    /// 1. Exact language match (case-insensitive)
    /// 2. Language-family match (first two letters match, e.g. "ru" matches "ru-RU" and vice versa)
    /// 3. Generic setting (Language == null)
    /// Returns null if no candidates exist.
    /// </summary>
    /// <param name="candidates">Settings sharing the same key but potentially different languages.</param>
    /// <param name="requestedLanguage">The requested language code (e.g. "ru", "ru-RU"), or null for generic only.</param>
    /// <returns>The best matching setting, or null.</returns>
    public static Setting? PickBestLanguageMatch(List<Setting> candidates, string? requestedLanguage)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var generic = candidates.FirstOrDefault(s => s.Language == null);

        if (string.IsNullOrWhiteSpace(requestedLanguage))
        {
            return generic;
        }

        // Try exact match first (case-insensitive)
        var exact = candidates.FirstOrDefault(s =>
            s.Language != null &&
            string.Equals(s.Language, requestedLanguage, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
        {
            return exact;
        }

        // Try fuzzy match: primary language subtag (first two characters)
        var requestedPrefix = GetLanguagePrefix(requestedLanguage);
        if (requestedPrefix != null)
        {
            var fuzzy = candidates.FirstOrDefault(s =>
                s.Language != null &&
                GetLanguagePrefix(s.Language) is string storedPrefix &&
                string.Equals(storedPrefix, requestedPrefix, StringComparison.OrdinalIgnoreCase));

            if (fuzzy != null)
            {
                return fuzzy;
            }
        }

        // Fall back to generic
        return generic;
    }

    /// <summary>
    /// Determines whether two language codes belong to the same language family
    /// by comparing their primary language subtag (first two characters, case-insensitive).
    /// </summary>
    /// <param name="language1">First language code.</param>
    /// <param name="language2">Second language code.</param>
    /// <returns>True if the codes share the same primary subtag.</returns>
    public static bool LanguageFamilyMatches(string? language1, string? language2)
    {
        if (language1 == null || language2 == null)
        {
            return false;
        }

        var prefix1 = GetLanguagePrefix(language1);
        var prefix2 = GetLanguagePrefix(language2);

        return prefix1 != null && prefix2 != null &&
               string.Equals(prefix1, prefix2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the primary language subtag (first two characters) from a language code.
    /// Returns null if the code is too short.
    /// </summary>
    /// <param name="language">Language code (e.g. "ru", "ru-RU", "en-US").</param>
    /// <returns>Two-character primary subtag, or null.</returns>
    private static string? GetLanguagePrefix(string language)
    {
        return language.Length >= 2 ? language[..2] : null;
    }
}
