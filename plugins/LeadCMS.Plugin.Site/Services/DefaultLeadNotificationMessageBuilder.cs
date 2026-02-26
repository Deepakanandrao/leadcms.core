// <copyright file="DefaultLeadNotificationMessageBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using LeadCMS.Plugin.Site.DTOs;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Default implementation for Site lead notification message formatting.
/// </summary>
public class DefaultLeadNotificationMessageBuilder : ILeadNotificationMessageBuilder
{
    /// <inheritdoc/>
    public virtual Dictionary<string, object> BuildEmailTemplateArguments(LeadNotificationInfo leadInfo)
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
    public virtual string BuildTextMessage(LeadNotificationInfo leadInfo)
    {
        var sb = new StringBuilder();

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

    protected static string? FormatTimeZoneOffset(int? offsetMinutes)
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
}
