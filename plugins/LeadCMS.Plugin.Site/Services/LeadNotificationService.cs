// <copyright file="LeadNotificationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net.Http.Json;
using System.Text;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Configuration;
using LeadCMS.Plugin.Site.DTOs;
using LeadCMS.Plugin.Site.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Service for sending lead capture notifications to various channels.
/// </summary>
public class LeadNotificationService : ILeadNotificationService
{
    private const int TelegramMessageMaxLength = 4096;
    private const int SlackMessageMaxLength = 40000;
    private readonly IEmailFromTemplateService emailService;
    private readonly ISettingService settingService;
    private readonly PluginSettings pluginSettings;
    private readonly ILogger<LeadNotificationService> logger;

    public LeadNotificationService(
        IEmailFromTemplateService emailService,
        ISettingService settingService,
        IConfiguration configuration,
        ILogger<LeadNotificationService> logger)
    {
        this.emailService = emailService;
        this.settingService = settingService;
        this.logger = logger;

        var settings = configuration.Get<PluginSettings>();
        pluginSettings = settings ?? new PluginSettings();
    }

    /// <summary>
    /// Builds template arguments from lead notification info for use in email templates.
    /// </summary>
    /// <param name="leadInfo">The lead notification information.</param>
    /// <returns>Dictionary of template arguments.</returns>
    public static Dictionary<string, object> BuildEmailTemplateArguments(LeadNotificationInfo leadInfo)
    {
        var templateArgs = new Dictionary<string, object>
        {
            { "email", leadInfo.Email },
            { "fromEmail", leadInfo.Email },
            { "firstName", leadInfo.FirstName ?? string.Empty },
            { "lastName", leadInfo.LastName ?? string.Empty },
            { "company", leadInfo.Company ?? string.Empty },
            { "subject", leadInfo.Subject ?? string.Empty },
            { "message", leadInfo.Message ?? string.Empty },
            { "title", leadInfo.Title ?? string.Empty },
        };

        if (!string.IsNullOrWhiteSpace(leadInfo.Phone))
        {
            templateArgs.Add("phone", leadInfo.Phone);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.PageUrl))
        {
            templateArgs.Add("pageUrl", leadInfo.PageUrl);
        }

        var timeZoneText = FormatTimeZoneOffset(leadInfo.TimeZoneOffset);
        if (!string.IsNullOrWhiteSpace(timeZoneText))
        {
            templateArgs.Add("timezone", timeZoneText);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.IpAddress))
        {
            templateArgs.Add("ipAddress", leadInfo.IpAddress);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.UserAgent))
        {
            templateArgs.Add("userAgent", leadInfo.UserAgent);
        }

        foreach (var item in leadInfo.ExtraData)
        {
            templateArgs.Add($"{item.Key}", item.Value);
            templateArgs.Add($"extraData[{item.Key}]", item.Value);
        }

        return templateArgs;
    }

    /// <inheritdoc/>
    public async Task SendLeadNotificationAsync(LeadNotificationInfo leadInfo, CancellationToken cancellationToken = default)
    {
        // Load all lead capture settings from the database
        var settings = await settingService.FindSettingsByKeysAsync(LeadCaptureSettingKeys.All, language: leadInfo.Language);

        var tasks = new List<Task>();

        // Send email notification (default: enabled)
        var emailEnabled = SettingListHelper.GetBool(settings, LeadCaptureSettingKeys.EmailEnabled, defaultValue: true);
        if (emailEnabled)
        {
            tasks.Add(SendEmailNotificationAsync(leadInfo, settings));
        }

        // Send Telegram notification (default: disabled)
        var telegramEnabled = SettingListHelper.GetBool(settings, LeadCaptureSettingKeys.TelegramEnabled, defaultValue: false);
        if (telegramEnabled)
        {
            tasks.Add(SendTelegramNotificationAsync(leadInfo, settings, cancellationToken));
        }

        // Send Slack notification (default: disabled)
        var slackEnabled = SettingListHelper.GetBool(settings, LeadCaptureSettingKeys.SlackEnabled, defaultValue: false);
        if (slackEnabled)
        {
            tasks.Add(SendSlackNotificationAsync(leadInfo, settings, cancellationToken));
        }

        // Wait for all notifications to complete
        await Task.WhenAll(tasks);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return MailboxAddress.TryParse(email, out _);
    }

    private static string BuildTextMessage(LeadNotificationInfo leadInfo)
    {
        var sb = new StringBuilder();

        // Use provided title or generate a sensible default
        var title = !string.IsNullOrWhiteSpace(leadInfo.Title)
            ? leadInfo.Title
            : "New lead captured";

        sb.AppendLine($"📩 {title}");
        sb.AppendLine($"✔️ Name: {leadInfo.FullName}");

        if (!string.IsNullOrWhiteSpace(leadInfo.Phone))
        {
            sb.AppendLine($"✔️ Phone: {leadInfo.Phone}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Company))
        {
            sb.AppendLine($"✔️ Company: {leadInfo.Company}");
        }

        sb.AppendLine($"✔️ Email: {leadInfo.Email}");

        if (!string.IsNullOrWhiteSpace(leadInfo.PageUrl))
        {
            sb.AppendLine($"✔️ Page URL: {leadInfo.PageUrl}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Subject))
        {
            sb.AppendLine($"✔️ Subject: {leadInfo.Subject}");
        }

        var timeZoneText = FormatTimeZoneOffset(leadInfo.TimeZoneOffset);
        if (!string.IsNullOrWhiteSpace(timeZoneText))
        {
            sb.AppendLine($"✔️ Timezone: {timeZoneText}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Language))
        {
            sb.AppendLine($"✔️ Language: {leadInfo.Language}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.IpAddress))
        {
            sb.AppendLine($"✔️ IP: {leadInfo.IpAddress}");
        }

        // if (!string.IsNullOrWhiteSpace(leadInfo.UserAgent))
        // {
        //     sb.AppendLine($"✔️ User-Agent: {leadInfo.UserAgent}");
        // }
        // Add any extra data
        foreach (var item in leadInfo.ExtraData)
        {
            sb.AppendLine($"✔️ {item.Key}: {item.Value}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Message))
        {
            sb.AppendLine($"✔️ Message: {leadInfo.Message}");
        }

        return sb.ToString().TrimEnd(',', ' ', '\n', '\r');
    }

    private static string? FormatTimeZoneOffset(int? offsetMinutes)
    {
        if (!offsetMinutes.HasValue)
        {
            return null;
        }

        var offset = TimeSpan.FromMinutes(offsetMinutes.Value);
        var sign = offset >= TimeSpan.Zero ? "+" : "-";
        var absolute = offset.Duration();
        return $"UTC{sign}{absolute:hh\\:mm}";
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || maxLength <= 0)
        {
            return string.Empty;
        }

        if (message.Length <= maxLength)
        {
            return message;
        }

        if (maxLength == 1)
        {
            return "…";
        }

        return message.Substring(0, maxLength - 1) + "…";
    }

    private async Task SendEmailNotificationAsync(LeadNotificationInfo leadInfo, List<Setting> settings)
    {
        try
        {
            // Determine target emails: use LeadCapture.Email.Recipients if set, otherwise fall back to ContactUs.To from plugin settings
            var leadCaptureEmails = SettingListHelper.GetStringArray(settings, LeadCaptureSettingKeys.EmailRecipients);
            var contactUsEmails = pluginSettings.ContactUs.To
                .Where(e => !string.IsNullOrEmpty(e) && !e.StartsWith('$'))
                .ToArray();

            var targetEmails = leadCaptureEmails.Length > 0 ? leadCaptureEmails : contactUsEmails;
            targetEmails = targetEmails.Where(IsValidEmail).ToArray();

            if (targetEmails.Length == 0)
            {
                logger.LogWarning("No valid email addresses configured for lead capture notifications");
                return;
            }

            var templateArgs = BuildEmailTemplateArguments(leadInfo);

            var templateName = string.IsNullOrWhiteSpace(leadInfo.NotificationType)
                ? "Contact_Us"
                : leadInfo.NotificationType;

            await emailService.SendAsync(
                templateName,
                leadInfo.Language ?? "en",
                targetEmails,
                templateArgs,
                leadInfo.Attachments,
                leadInfo.ContactId ?? 0);

            logger.LogInformation("Lead notification email sent successfully to {Recipients}", string.Join(", ", targetEmails));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send lead notification email");
            throw;
        }
    }

    private async Task SendTelegramNotificationAsync(LeadNotificationInfo leadInfo, List<Setting> settings, CancellationToken cancellationToken)
    {
        var botId = SettingListHelper.GetString(settings, LeadCaptureSettingKeys.TelegramBotId);
        var chatId = SettingListHelper.GetString(settings, LeadCaptureSettingKeys.TelegramChatId);

        if (string.IsNullOrEmpty(botId) || string.IsNullOrEmpty(chatId))
        {
            logger.LogWarning("Telegram bot ID or chat ID is not configured for lead capture notifications");
            return;
        }

        try
        {
            var message = BuildTextMessage(leadInfo);
            message = TruncateMessage(message, TelegramMessageMaxLength);

            using var httpClient = new HttpClient();

            var sendMessageUrl = $"https://api.telegram.org/bot{botId}/sendMessage";
            using var messageContent = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("chat_id", chatId),
                new KeyValuePair<string, string>("text", message),
            ]);

            var response = await httpClient.PostAsync(sendMessageUrl, messageContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new TelegramException($"Failed to send message to Telegram chat. Status Code: {response.StatusCode}. Response: {content}");
            }

            if (leadInfo.Attachments is { Count: > 0 })
            {
                var sendDocumentUrl = $"https://api.telegram.org/bot{botId}/sendDocument";

                foreach (var attachment in leadInfo.Attachments)
                {
                    if (attachment?.File == null || attachment.File.Length == 0)
                    {
                        continue;
                    }

                    using var multipart = new MultipartFormDataContent();
                    multipart.Add(new StringContent(chatId), "chat_id");

                    var fileContent = new ByteArrayContent(attachment.File);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    multipart.Add(fileContent, "document", attachment.FileName);

                    var docResponse = await httpClient.PostAsync(sendDocumentUrl, multipart, cancellationToken);
                    if (!docResponse.IsSuccessStatusCode)
                    {
                        var docContent = await docResponse.Content.ReadAsStringAsync(cancellationToken);
                        throw new TelegramException($"Failed to send document to Telegram chat. Status Code: {docResponse.StatusCode}. Response: {docContent}");
                    }
                }
            }

            logger.LogInformation("Lead notification sent to Telegram successfully");
        }
        catch (TelegramException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send lead notification to Telegram");
            throw new TelegramException("Failed to send message to Telegram", ex);
        }
    }

    private async Task SendSlackNotificationAsync(LeadNotificationInfo leadInfo, List<Setting> settings, CancellationToken cancellationToken)
    {
        var webhookUrl = SettingListHelper.GetString(settings, LeadCaptureSettingKeys.SlackWebhookUrl);

        if (string.IsNullOrEmpty(webhookUrl))
        {
            logger.LogWarning("Slack webhook URL is not configured for lead capture notifications");
            return;
        }

        try
        {
            var message = BuildTextMessage(leadInfo);
            message = TruncateMessage(message, SlackMessageMaxLength);

            var payload = new SlackMessagePayload
            {
                Text = message,
            };

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new SlackException($"Failed to send message to Slack. Status Code: {response.StatusCode}. Response: {content}");
            }

            logger.LogInformation("Lead notification sent to Slack successfully");
        }
        catch (SlackException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send lead notification to Slack");
            throw new SlackException("Failed to send message to Slack", ex);
        }
    }

    private sealed class SlackMessagePayload
    {
        public string Text { get; set; } = string.Empty;
    }
}
