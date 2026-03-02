// <copyright file="DefaultLeadNotificationMessageBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using LeadCMS.Plugin.Site.DTOs;
using UAParser;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Default implementation for Site lead notification message formatting.
/// </summary>
public class DefaultLeadNotificationMessageBuilder : ILeadNotificationMessageBuilder
{
    /// <inheritdoc/>
    public virtual Dictionary<string, object> BuildEmailTemplateArguments(LeadNotificationInfo leadInfo)
    {
        var templateArgs = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(leadInfo.Email))
        {
            templateArgs.Add("email", leadInfo.Email);
            templateArgs.Add("fromEmail", leadInfo.Email);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.FirstName))
        {
            templateArgs.Add("firstName", leadInfo.FirstName);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.LastName))
        {
            templateArgs.Add("lastName", leadInfo.LastName);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.FullName))
        {
            templateArgs.Add("fullName", leadInfo.FullName);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Company))
        {
            templateArgs.Add("company", leadInfo.Company);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Subject))
        {
            templateArgs.Add("subject", leadInfo.Subject);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Message))
        {
            templateArgs.Add("message", leadInfo.Message);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Title))
        {
            templateArgs.Add("title", leadInfo.Title);
        }

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
            templateArgs.Add("timeZoneOffset", timeZoneText);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.IpAddress))
        {
            templateArgs.Add("ipAddress", leadInfo.IpAddress);
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.UserAgent))
        {
            templateArgs.Add("userAgent", leadInfo.UserAgent);

            try
            {
                var clientInfo = Parser.GetDefault().Parse(leadInfo.UserAgent);

                if (!string.IsNullOrWhiteSpace(clientInfo.UA.Family))
                {
                    templateArgs["userAgentFamily"] = clientInfo.UA.Family;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.UA.Major))
                {
                    templateArgs["userAgentMajor"] = clientInfo.UA.Major;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.UA.Minor))
                {
                    templateArgs["userAgentMinor"] = clientInfo.UA.Minor;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.UA.Patch))
                {
                    templateArgs["userAgentPatch"] = clientInfo.UA.Patch;
                }

                var userAgentVersion = ComposeVersion(clientInfo.UA.Major, clientInfo.UA.Minor, clientInfo.UA.Patch);
                if (!string.IsNullOrWhiteSpace(userAgentVersion))
                {
                    templateArgs["userAgentVersion"] = userAgentVersion;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.OS.Family))
                {
                    templateArgs["osFamily"] = clientInfo.OS.Family;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.OS.Major))
                {
                    templateArgs["osMajor"] = clientInfo.OS.Major;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.OS.Minor))
                {
                    templateArgs["osMinor"] = clientInfo.OS.Minor;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.OS.Patch))
                {
                    templateArgs["osPatch"] = clientInfo.OS.Patch;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.OS.PatchMinor))
                {
                    templateArgs["osPatchMinor"] = clientInfo.OS.PatchMinor;
                }

                var osVersion = ComposeVersion(clientInfo.OS.Major, clientInfo.OS.Minor, clientInfo.OS.Patch, clientInfo.OS.PatchMinor);
                if (!string.IsNullOrWhiteSpace(osVersion))
                {
                    templateArgs["osVersion"] = osVersion;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.Device.Family))
                {
                    templateArgs["deviceFamily"] = clientInfo.Device.Family;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.Device.Brand))
                {
                    templateArgs["deviceBrand"] = clientInfo.Device.Brand;
                }

                if (!string.IsNullOrWhiteSpace(clientInfo.Device.Model))
                {
                    templateArgs["deviceModel"] = clientInfo.Device.Model;
                }

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
                    templateArgs["userDeviceSummary"] = userDeviceSummary;
                }
            }
            catch (Exception)
            {
                templateArgs["userAgentParseFailed"] = true;
                templateArgs["userDeviceSummary"] = leadInfo.UserAgent;
            }
        }

        if (!string.IsNullOrWhiteSpace(leadInfo.Language))
        {
            templateArgs.Add("language", leadInfo.Language);
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
