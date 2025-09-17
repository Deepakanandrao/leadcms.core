// <copyright file="SettingKeys.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Constants;

/// <summary>
/// Defines setting keys for content validation runtime configuration.
/// These keys can be used with the SettingsController to override default configuration values.
/// </summary>
public static class SettingKeys
{
    public const string PreviewUrlTemplate = "PreviewUrlTemplate";

    public const string LivePreviewUrlTemplate = "LivePreviewUrlTemplate";

    public const string MaxTitleLength = "Content.MaxTitleLength";

    public const string MaxDescriptionLength = "Content.MaxDescriptionLength";
}