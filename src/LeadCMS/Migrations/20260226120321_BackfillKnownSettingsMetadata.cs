using LeadCMS.Constants;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadCMS.Migrations
{
    /// <inheritdoc />
    public partial class BackfillKnownSettingsMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var metadataUpdates = new (string Key, bool Required, string Type, string Description)[]
            {
                ("PreviewUrlTemplate", false, SettingValueTypes.Text, "Preview URL template used for content preview links."),
                ("LivePreviewUrlTemplate", false, SettingValueTypes.Text, "Live preview URL template used for published content links."),
                ("Content.MinTitleLength", false, SettingValueTypes.Int, "Minimum allowed title length for content."),
                ("Content.MaxTitleLength", false, SettingValueTypes.Int, "Maximum allowed title length for content."),
                ("Content.MinDescriptionLength", false, SettingValueTypes.Int, "Minimum allowed description length for content."),
                ("Content.MaxDescriptionLength", false, SettingValueTypes.Int, "Maximum allowed description length for content."),
                ("Content.EnableRealtimeSyntaxValidation", false, SettingValueTypes.Bool, "Enables real-time content syntax validation in the editor."),
                ("Content.EnableCodeEditorLineNumbers", false, SettingValueTypes.Bool, "Shows line numbers in the content code editor."),
                ("Identity.RequireDigit", false, SettingValueTypes.Bool, "Require at least one digit in user passwords."),
                ("Identity.RequireUppercase", false, SettingValueTypes.Bool, "Require at least one uppercase character in user passwords."),
                ("Identity.RequireLowercase", false, SettingValueTypes.Bool, "Require at least one lowercase character in user passwords."),
                ("Identity.RequireNonAlphanumeric", false, SettingValueTypes.Bool, "Require at least one non-alphanumeric character in user passwords."),
                ("Identity.RequiredLength", false, SettingValueTypes.Int, "Minimum password length."),
                ("Identity.RequiredUniqueChars", false, SettingValueTypes.Int, "Minimum number of unique characters in a password."),
                ("Media.Cover.Dimensions", false, SettingValueTypes.Text, "Target dimensions for generated cover images (e.g. 512x256)."),
                ("Media.Max.Dimensions", false, SettingValueTypes.Text, "Maximum media dimensions allowed for optimization."),
                ("Media.PreferredFormat", false, SettingValueTypes.Text, "Preferred image output format for optimized media."),
                ("Media.Max.FileSize", false, SettingValueTypes.Int, "Maximum media file size in KB."),
                ("Media.EnableOptimisation", false, SettingValueTypes.Bool, "Enable media optimization pipeline."),
                ("Media.Quality", false, SettingValueTypes.Int, "Default output quality for optimized media."),
                ("Media.EnableCoverResize", false, SettingValueTypes.Bool, "Enable cover image resize to configured cover dimensions."),
                ("ApiSettings.MaxListSize", false, SettingValueTypes.Int, "Maximum number of records returned by list endpoints."),
                ("ApiSettings.DefaultFromEmail", false, SettingValueTypes.Text, "Default sender email address used for system-generated emails."),
                ("ApiSettings.DefaultFromName", false, SettingValueTypes.Text, "Default sender display name used for system-generated emails."),
                ("AI.SiteProfile.Topic", false, SettingValueTypes.TextArea, "Main site topic used to guide AI-generated content and templates."),
                ("AI.SiteProfile.Audience", false, SettingValueTypes.TextArea, "Target audience profile used for AI-generated content and templates."),
                ("AI.SiteProfile.BrandVoice", false, SettingValueTypes.TextArea, "Brand voice and tone guidance for AI-generated outputs."),
                ("AI.SiteProfile.PreferredTerms", false, SettingValueTypes.TextArea, "Preferred terminology that AI should favor."),
                ("AI.SiteProfile.AvoidTerms", false, SettingValueTypes.TextArea, "Terminology that AI should avoid."),
                ("AI.SiteProfile.StyleExamples", false, SettingValueTypes.TextArea, "Examples of desired writing style for AI outputs."),
                ("AI.SiteProfile.BlogCover.Instructions", false, SettingValueTypes.TextArea, "Additional instructions for AI-generated blog cover images."),
                ("AI.SiteProfile.EmailTemplate.Instructions", false, SettingValueTypes.TextArea, "Additional instructions for AI-generated email templates."),
                ("LeadCapture.Email.Enabled", false, SettingValueTypes.Bool, "Whether email notifications are enabled for lead capture."),
                ("LeadCapture.Email.Recipients", false, SettingValueTypes.EmailArray, "Array of email addresses to send lead notifications to."),
                ("LeadCapture.Telegram.Enabled", false, SettingValueTypes.Bool, "Whether Telegram notifications are enabled for lead capture."),
                ("LeadCapture.Telegram.BotId", true, SettingValueTypes.Text, "The Telegram bot ID for sending lead notifications."),
                ("LeadCapture.Telegram.ChatId", true, SettingValueTypes.Text, "The Telegram chat ID to send lead notifications to."),
                ("LeadCapture.Slack.Enabled", false, SettingValueTypes.Bool, "Whether Slack notifications are enabled for lead capture."),
                ("LeadCapture.Slack.WebhookUrl", true, SettingValueTypes.Text, "The Slack incoming webhook URL for sending lead notifications."),
            };

            foreach (var metadata in metadataUpdates)
            {
                var key = metadata.Key.Replace("'", "''", StringComparison.Ordinal);
                var type = metadata.Type.Replace("'", "''", StringComparison.Ordinal);
                var description = metadata.Description.Replace("'", "''", StringComparison.Ordinal);
                var required = metadata.Required ? "TRUE" : "FALSE";

                migrationBuilder.Sql($@"
UPDATE setting
SET required = {required},
    type = '{type}',
    description = '{description}'
WHERE key = '{key}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE setting
SET required = FALSE,
    type = NULL,
    description = NULL
WHERE key IN (
    'PreviewUrlTemplate',
    'LivePreviewUrlTemplate',
    'Content.MinTitleLength',
    'Content.MaxTitleLength',
    'Content.MinDescriptionLength',
    'Content.MaxDescriptionLength',
    'Content.EnableRealtimeSyntaxValidation',
    'Content.EnableCodeEditorLineNumbers',
    'Identity.RequireDigit',
    'Identity.RequireUppercase',
    'Identity.RequireLowercase',
    'Identity.RequireNonAlphanumeric',
    'Identity.RequiredLength',
    'Identity.RequiredUniqueChars',
    'Media.Cover.Dimensions',
    'Media.Max.Dimensions',
    'Media.PreferredFormat',
    'Media.Max.FileSize',
    'Media.EnableOptimisation',
    'Media.Quality',
    'Media.EnableCoverResize',
    'ApiSettings.MaxListSize',
    'ApiSettings.DefaultFromEmail',
    'ApiSettings.DefaultFromName',
    'AI.SiteProfile.Topic',
    'AI.SiteProfile.Audience',
    'AI.SiteProfile.BrandVoice',
    'AI.SiteProfile.PreferredTerms',
    'AI.SiteProfile.AvoidTerms',
    'AI.SiteProfile.StyleExamples',
    'AI.SiteProfile.BlogCover.Instructions',
    'AI.SiteProfile.EmailTemplate.Instructions',
    'LeadCapture.Email.Enabled',
    'LeadCapture.Email.Recipients',
    'LeadCapture.Telegram.Enabled',
    'LeadCapture.Telegram.BotId',
    'LeadCapture.Telegram.ChatId',
    'LeadCapture.Slack.Enabled',
    'LeadCapture.Slack.WebhookUrl'
);");
        }
    }
}
