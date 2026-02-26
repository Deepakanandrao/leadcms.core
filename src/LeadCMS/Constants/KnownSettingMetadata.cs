// <copyright file="KnownSettingMetadata.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.Configuration;

namespace LeadCMS.Constants;

public sealed class SettingMetadataDefinition
{
    public SettingMetadataDefinition(string key, bool required, string type, string description, string? defaultValue = null)
    {
        Key = key;
        Required = required;
        Type = type;
        Description = description;
        DefaultValue = defaultValue;
    }

    public string Key { get; }

    public bool Required { get; }

    public string Type { get; }

    public string Description { get; }

    public string? DefaultValue { get; }
}

public static class KnownSettingMetadata
{
    public static IReadOnlyList<SettingMetadataDefinition> All { get; } = new List<SettingMetadataDefinition>
    {
        new(SettingKeys.PreviewUrlTemplate, false, "string", "Preview URL template used for content preview links."),
        new(SettingKeys.LivePreviewUrlTemplate, false, "string", "Live preview URL template used for published content links."),
        new(SettingKeys.MinTitleLength, false, "int", "Minimum allowed title length for content."),
        new(SettingKeys.MaxTitleLength, false, "int", "Maximum allowed title length for content."),
        new(SettingKeys.MinDescriptionLength, false, "int", "Minimum allowed description length for content."),
        new(SettingKeys.MaxDescriptionLength, false, "int", "Maximum allowed description length for content."),
        new(SettingKeys.EnableRealtimeSyntaxValidation, false, "bool", "Enables real-time content syntax validation in the editor."),
        new(SettingKeys.EnableCodeEditorLineNumbers, false, "bool", "Shows line numbers in the content code editor."),

        new(SettingKeys.RequireDigit, false, "bool", "Require at least one digit in user passwords."),
        new(SettingKeys.RequireUppercase, false, "bool", "Require at least one uppercase character in user passwords."),
        new(SettingKeys.RequireLowercase, false, "bool", "Require at least one lowercase character in user passwords."),
        new(SettingKeys.RequireNonAlphanumeric, false, "bool", "Require at least one non-alphanumeric character in user passwords."),
        new(SettingKeys.RequiredLength, false, "int", "Minimum password length."),
        new(SettingKeys.RequiredUniqueChars, false, "int", "Minimum number of unique characters in a password."),

        new(SettingKeys.MediaCoverDimensions, false, "string", "Target dimensions for generated cover images (e.g. 512x256)."),
        new(SettingKeys.MediaMaxDimensions, false, "string", "Maximum media dimensions allowed for optimization."),
        new(SettingKeys.MediaPreferredFormat, false, "string", "Preferred image output format for optimized media."),
        new(SettingKeys.MediaMaxFileSize, false, "int", "Maximum media file size in KB."),
        new(SettingKeys.MediaEnableOptimisation, false, "bool", "Enable media optimization pipeline."),
        new(SettingKeys.MediaQuality, false, "int", "Default output quality for optimized media."),
        new(SettingKeys.MediaEnableCoverResize, false, "bool", "Enable cover image resize to configured cover dimensions."),

        new("ApiSettings.MaxListSize", false, "int", "Maximum number of records returned by list endpoints."),
        new("ApiSettings.DefaultFromEmail", false, "string", "Default sender email address used for system-generated emails."),
        new("ApiSettings.DefaultFromName", false, "string", "Default sender display name used for system-generated emails."),

        new(AiSettingKeys.SiteTopic, false, "string", "Main site topic used to guide AI-generated content and templates."),
        new(AiSettingKeys.SiteAudience, false, "string", "Target audience profile used for AI-generated content and templates."),
        new(AiSettingKeys.BrandVoice, false, "string", "Brand voice and tone guidance for AI-generated outputs."),
        new(AiSettingKeys.PreferredTerms, false, "string", "Preferred terminology that AI should favor."),
        new(AiSettingKeys.AvoidTerms, false, "string", "Terminology that AI should avoid."),
        new(AiSettingKeys.StyleExamples, false, "string", "Examples of desired writing style for AI outputs."),
        new(AiSettingKeys.BlogCoverInstructions, false, "string", "Additional instructions for AI-generated blog cover images."),
        new(AiSettingKeys.EmailTemplateInstructions, false, "string", "Additional instructions for AI-generated email templates."),

        new("LeadCapture.Email.Enabled", false, "bool", "Whether email notifications are enabled for lead capture.", "false"),
        new("LeadCapture.Email.Recipients", false, "json", "JSON array of email addresses to send lead notifications to.", "[]"),
        new("LeadCapture.Telegram.Enabled", false, "bool", "Whether Telegram notifications are enabled for lead capture.", "false"),
        new("LeadCapture.Telegram.BotId", true, "string", "The Telegram bot ID for sending lead notifications.", string.Empty),
        new("LeadCapture.Telegram.ChatId", true, "string", "The Telegram chat ID to send lead notifications to.", string.Empty),
        new("LeadCapture.Slack.Enabled", false, "bool", "Whether Slack notifications are enabled for lead capture.", "false"),
        new("LeadCapture.Slack.WebhookUrl", true, "string", "The Slack incoming webhook URL for sending lead notifications.", string.Empty),
    }.AsReadOnly();

    public static bool TryGet(string key, out SettingMetadataDefinition definition)
    {
        definition = All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? new SettingMetadataDefinition(string.Empty, false, string.Empty, string.Empty);
        return !string.IsNullOrEmpty(definition.Key);
    }
}
