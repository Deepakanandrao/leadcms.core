// <copyright file="CoreSettingsProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using LeadCMS.Interfaces;

namespace LeadCMS.Configuration;

/// <summary>
/// Registers core (non-plugin) settings using the same provider pipeline as plugin settings.
/// </summary>
public class CoreSettingsProvider : ISettingsProvider
{
    /// <inheritdoc/>
    public IEnumerable<SettingDefinition> GetSettingDefinitions()
    {
        foreach (var definition in KnownSettingMetadata.All)
        {
            yield return new SettingDefinition
            {
                Key = definition.Key,
                DefaultValue = definition.DefaultValue,
                Required = definition.Required,
                Type = definition.Type,
                Description = definition.Description,
            };
        }
    }
}
