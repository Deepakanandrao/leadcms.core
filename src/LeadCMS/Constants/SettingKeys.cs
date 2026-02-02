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

    public const string MinTitleLength = "Content.MinTitleLength";

    public const string MaxTitleLength = "Content.MaxTitleLength";

    public const string MinDescriptionLength = "Content.MinDescriptionLength";

    public const string MaxDescriptionLength = "Content.MaxDescriptionLength";

    public const string EnableRealtimeSyntaxValidation = "Content.EnableRealtimeSyntaxValidation";

    public const string EnableCodeEditorLineNumbers = "Content.EnableCodeEditorLineNumbers";

    // Identity password policy settings
    public const string RequireDigit = "Identity.RequireDigit";

    public const string RequireUppercase = "Identity.RequireUppercase";

    public const string RequireLowercase = "Identity.RequireLowercase";

    public const string RequireNonAlphanumeric = "Identity.RequireNonAlphanumeric";

    public const string RequiredLength = "Identity.RequiredLength";

    public const string RequiredUniqueChars = "Identity.RequiredUniqueChars";

    // Media optimization settings
    public const string MediaCoverDimensions = "Media.Cover.Dimensions";

    public const string MediaMaxDimensions = "Media.Max.Dimensions";

    public const string MediaPreferredFormat = "Media.PreferredFormat";

    public const string MediaMaxFileSize = "Media.Max.FileSize";

    public const string MediaEnableOptimisation = "Media.EnableOptimisation";

    public const string MediaQuality = "Media.Quality";

    public const string MediaEnableCoverResize = "Media.EnableCoverResize";
}

public static class ConfigurationPaths
{
    public static string GetConfigurationPath(string settingKey)
    {
        return settingKey.Replace(".", ":");
    }
}