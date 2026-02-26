// <copyright file="LeadCaptureSettingKeys.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.Site.Configuration;

/// <summary>
/// Setting keys for lead capture notification configuration.
/// These settings are stored in the database and can be configured via the Settings API.
/// </summary>
public static class LeadCaptureSettingKeys
{
    /// <summary>
    /// Whether email notifications are enabled for lead capture.
    /// </summary>
    public const string EmailEnabled = "LeadCapture.Email.Enabled";

    /// <summary>
    /// JSON array of email addresses to send lead notifications to.
    /// Falls back to ContactUs.To if not set.
    /// </summary>
    public const string EmailRecipients = "LeadCapture.Email.Recipients";

    /// <summary>
    /// Whether Telegram notifications are enabled for lead capture.
    /// </summary>
    public const string TelegramEnabled = "LeadCapture.Telegram.Enabled";

    /// <summary>
    /// The Telegram bot ID for sending notifications.
    /// </summary>
    public const string TelegramBotId = "LeadCapture.Telegram.BotId";

    /// <summary>
    /// The Telegram chat ID to send notifications to.
    /// </summary>
    public const string TelegramChatId = "LeadCapture.Telegram.ChatId";

    /// <summary>
    /// Whether Slack notifications are enabled for lead capture.
    /// </summary>
    public const string SlackEnabled = "LeadCapture.Slack.Enabled";

    /// <summary>
    /// The Slack incoming webhook URL for sending notifications.
    /// </summary>
    public const string SlackWebhookUrl = "LeadCapture.Slack.WebhookUrl";

    /// <summary>
    /// Gets all lead capture setting keys.
    /// </summary>
    public static IEnumerable<string> All => new[]
    {
        EmailEnabled,
        EmailRecipients,
        TelegramEnabled,
        TelegramBotId,
        TelegramChatId,
        SlackEnabled,
        SlackWebhookUrl,
    };
}
