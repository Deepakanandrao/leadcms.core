// <copyright file="UserAgentDeviceSummaryHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using UAParser;

namespace LeadCMS.Helpers;

public static class UserAgentDeviceSummaryHelper
{
    public static string? Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        try
        {
            var clientInfo = Parser.GetDefault().Parse(userAgent);

            var userAgentVersion = ComposeVersion(
                clientInfo.UA.Major,
                clientInfo.UA.Minor,
                clientInfo.UA.Patch);

            var osVersion = ComposeVersion(
                clientInfo.OS.Major,
                clientInfo.OS.Minor,
                clientInfo.OS.Patch,
                clientInfo.OS.PatchMinor);

            var summary = BuildUserDeviceSummary(
                clientInfo.Device.Brand,
                clientInfo.Device.Model,
                clientInfo.Device.Family,
                clientInfo.OS.Family,
                osVersion,
                clientInfo.UA.Family,
                userAgentVersion);

            return string.IsNullOrWhiteSpace(summary) ? null : summary;
        }
        catch (Exception)
        {
            return userAgent;
        }
    }

    private static string? ComposeVersion(params string?[] versionParts)
    {
        var parts = versionParts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? null : string.Join('.', parts);
    }

    private static string BuildUserDeviceSummary(
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