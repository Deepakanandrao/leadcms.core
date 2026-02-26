// <copyright file="IPluginSettingsProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;

namespace LeadCMS.Interfaces;

/// <summary>
/// Interface for any module (core or plugin) to declare settings it requires.
/// The core will automatically surface these settings in the admin settings API
/// so clients can discover and edit them.
/// </summary>
public interface ISettingsProvider
{
    /// <summary>
    /// Gets the setting definitions that this module requires.
    /// </summary>
    /// <returns>A collection of setting definitions.</returns>
    IEnumerable<SettingDefinition> GetSettingDefinitions();
}

/// <summary>
/// Describes a single setting that is registered with the core.
/// </summary>
public class SettingDefinition
{
    /// <summary>
    /// Gets or sets the setting key, using dot-notation (e.g. "LeadCapture.Telegram.BotId").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default value for this setting. Null if no default.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this setting is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the data type of the setting (e.g. "text", "textarea", "bool", "int", "email[]").
    /// </summary>
    public string Type { get; set; } = SettingValueTypes.Text;

    /// <summary>
    /// Gets or sets a human-readable description for the admin UI.
    /// </summary>
    public string? Description { get; set; }
}
