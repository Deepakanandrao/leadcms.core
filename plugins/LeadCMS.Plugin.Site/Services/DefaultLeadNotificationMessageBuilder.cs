// <copyright file="DefaultLeadNotificationMessageBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using LeadCMS.Helpers;
using LeadCMS.Plugin.Site.DTOs;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Default implementation for Site lead notification message formatting.
/// Enriches template arguments with user-agent device summary and builds
/// plain-text messages for Telegram/Slack.
/// </summary>
public class DefaultLeadNotificationMessageBuilder : ILeadNotificationMessageBuilder
{
    /// <inheritdoc/>
    public virtual void EnrichTemplateArguments(Dictionary<string, object> args, LeadNotificationInfo leadInfo)
    {
        if (string.IsNullOrWhiteSpace(leadInfo.UserAgent))
        {
            return;
        }

        try
        {
            var userDeviceSummary = UserAgentDeviceSummaryHelper.Parse(leadInfo.UserAgent);

            if (!string.IsNullOrWhiteSpace(userDeviceSummary))
            {
                args["UserDeviceSummary"] = userDeviceSummary;
            }
        }
        catch (Exception)
        {
            args["UserDeviceSummary"] = leadInfo.UserAgent;
        }
    }

    /// <inheritdoc/>
    public virtual string BuildTextMessage(LeadNotificationInfo leadInfo)
    {
        var sb = new StringBuilder();

        var title = !string.IsNullOrWhiteSpace(leadInfo.Title)
            ? leadInfo.Title
            : "New lead captured";

        sb.AppendLine($"📩 {title}");

        if (!string.IsNullOrWhiteSpace(leadInfo.FullName))
        {
            sb.AppendLine($"✔️ Name: {leadInfo.FullName}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Phone))
        {
            sb.AppendLine($"✔️ Phone: {leadInfo.Phone}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.CompanyName))
        {
            sb.AppendLine($"✔️ Company: {leadInfo.CompanyName}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Email))
        {
            sb.AppendLine($"✔️ Email: {leadInfo.Email}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.PageUrl))
        {
            sb.AppendLine($"✔️ Page URL: {leadInfo.PageUrl}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Subject))
        {
            sb.AppendLine($"✔️ Subject: {leadInfo.Subject}");
        }

        if (leadInfo.Timezone.HasValue)
        {
            sb.AppendLine($"✔️ Timezone: {TimezoneHelper.FormatUtcOffset(leadInfo.Timezone.Value)}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Language))
        {
            sb.AppendLine($"✔️ Language: {leadInfo.Language}");
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.IpAddressV4))
        {
            sb.AppendLine($"✔️ IP: {leadInfo.IpAddressV4}");
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
}
