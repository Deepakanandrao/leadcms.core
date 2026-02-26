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
                ("PreviewUrlTemplate", false, "string", "Preview URL template used for content preview links."),
                ("LivePreviewUrlTemplate", false, "string", "Live preview URL template used for published content links."),
                ("Content.MinTitleLength", false, "int", "Minimum allowed title length for content."),
                ("Content.MaxTitleLength", false, "int", "Maximum allowed title length for content."),
                ("Content.MinDescriptionLength", false, "int", "Minimum allowed description length for content."),
                ("Content.MaxDescriptionLength", false, "int", "Maximum allowed description length for content."),
                ("Content.EnableRealtimeSyntaxValidation", false, "bool", "Enables real-time content syntax validation in the editor."),
                ("Content.EnableCodeEditorLineNumbers", false, "bool", "Shows line numbers in the content code editor."),
                ("Identity.RequireDigit", false, "bool", "Require at least one digit in user passwords."),
                ("Identity.RequireUppercase", false, "bool", "Require at least one uppercase character in user passwords."),
                ("Identity.RequireLowercase", false, "bool", "Require at least one lowercase character in user passwords."),
                ("Identity.RequireNonAlphanumeric", false, "bool", "Require at least one non-alphanumeric character in user passwords."),
                ("Identity.RequiredLength", false, "int", "Minimum password length."),
                ("Identity.RequiredUniqueChars", false, "int", "Minimum number of unique characters in a password."),
                ("Media.Cover.Dimensions", false, "string", "Target dimensions for generated cover images (e.g. 512x256)."),
                ("Media.Max.Dimensions", false, "string", "Maximum media dimensions allowed for optimization."),
                ("Media.PreferredFormat", false, "string", "Preferred image output format for optimized media."),
                ("Media.Max.FileSize", false, "int", "Maximum media file size in KB."),
                ("Media.EnableOptimisation", false, "bool", "Enable media optimization pipeline."),
                ("Media.Quality", false, "int", "Default output quality for optimized media."),
                ("Media.EnableCoverResize", false, "bool", "Enable cover image resize to configured cover dimensions."),
                ("ApiSettings.MaxListSize", false, "int", "Maximum number of records returned by list endpoints."),
                ("ApiSettings.DefaultFromEmail", false, "string", "Default sender email address used for system-generated emails."),
                ("ApiSettings.DefaultFromName", false, "string", "Default sender display name used for system-generated emails."),
                ("AI.SiteProfile.Topic", false, "string", "Main site topic used to guide AI-generated content and templates."),
                ("AI.SiteProfile.Audience", false, "string", "Target audience profile used for AI-generated content and templates."),
                ("AI.SiteProfile.BrandVoice", false, "string", "Brand voice and tone guidance for AI-generated outputs."),
                ("AI.SiteProfile.PreferredTerms", false, "string", "Preferred terminology that AI should favor."),
                ("AI.SiteProfile.AvoidTerms", false, "string", "Terminology that AI should avoid."),
                ("AI.SiteProfile.StyleExamples", false, "string", "Examples of desired writing style for AI outputs."),
                ("AI.SiteProfile.BlogCover.Instructions", false, "string", "Additional instructions for AI-generated blog cover images."),
                ("AI.SiteProfile.EmailTemplate.Instructions", false, "string", "Additional instructions for AI-generated email templates."),
                ("LeadCapture.Email.Enabled", false, "bool", "Whether email notifications are enabled for lead capture."),
                ("LeadCapture.Email.Recipients", false, "email[]", "Array of email addresses to send lead notifications to."),
                ("LeadCapture.Telegram.Enabled", false, "bool", "Whether Telegram notifications are enabled for lead capture."),
                ("LeadCapture.Telegram.BotId", true, "string", "The Telegram bot ID for sending lead notifications."),
                ("LeadCapture.Telegram.ChatId", true, "string", "The Telegram chat ID to send lead notifications to."),
                ("LeadCapture.Slack.Enabled", false, "bool", "Whether Slack notifications are enabled for lead capture."),
                ("LeadCapture.Slack.WebhookUrl", true, "string", "The Slack incoming webhook URL for sending lead notifications."),
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
