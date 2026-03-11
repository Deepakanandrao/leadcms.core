// <copyright file="DefaultLeadNotificationMessageBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using LeadCMS.Helpers;
using LeadCMS.Plugin.Site.DTOs;
using UAParser;

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
            var clientInfo = Parser.GetDefault().Parse(leadInfo.UserAgent);

            var userAgentVersion = ComposeVersion(clientInfo.UA.Major, clientInfo.UA.Minor, clientInfo.UA.Patch);

            var osVersion = ComposeVersion(clientInfo.OS.Major, clientInfo.OS.Minor, clientInfo.OS.Patch, clientInfo.OS.PatchMinor);

            var userDeviceSummary = BuildUserDeviceSummary(
                clientInfo.Device.Brand,
                clientInfo.Device.Model,
                clientInfo.Device.Family,
                clientInfo.OS.Family,
                osVersion,
                clientInfo.UA.Family,
                userAgentVersion);

            if (!string.IsNullOrWhiteSpace(userDeviceSummary))
            {
                args["userDeviceSummary"] = userDeviceSummary;
            }
        }
        catch (Exception)
        {
            args["userAgentParseFailed"] = true;
            args["userDeviceSummary"] = leadInfo.UserAgent;
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

    protected static string? ComposeVersion(params string?[] versionParts)
    {
        var parts = versionParts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? null : string.Join('.', parts);
    }

    protected static string BuildUserDeviceSummary(
        string? deviceBrand,
        string? deviceModel,
        string? deviceFamily,
        string? osFamily,
        string? osVersion,
        string? browserFamily,
        string? browserVersion)
    {
        var deviceName = string.Join(
            ' ',
            new[] { deviceBrand, deviceModel }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            deviceName = deviceFamily?.Trim();
        }

        var osName = string.Join(
            ' ',
            new[] { osFamily, osVersion }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));

        var browserName = string.Join(
            ' ',
            new[] { browserFamily, browserVersion }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));

        var segments = new[]
        {
            string.IsNullOrWhiteSpace(deviceName) ? null : deviceName,
            string.IsNullOrWhiteSpace(osName) ? null : osName,
            string.IsNullOrWhiteSpace(browserName) ? null : browserName,
        }
        .Where(part => !string.IsNullOrWhiteSpace(part))
        .ToArray();

        return segments.Length == 0 ? string.Empty : string.Join(" • ", segments);
    }
}
