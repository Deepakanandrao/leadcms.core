// <copyright file="MediaOptimizationHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Helpers;

public static class MediaOptimizationHelper
{
    private static readonly HashSet<string> NonOptimizableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ico",
        ".gif",
        ".svg",
        ".svgz",
        ".apng",
        ".ani",
    };

    private static readonly HashSet<string> NonOptimizableMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/x-icon",
        "image/vnd.microsoft.icon",
        "image/gif",
        "image/svg+xml",
        "image/apng",
    };

    public static bool ShouldSkipOptimization(string extension, string mimeType)
    {
        if (!string.IsNullOrWhiteSpace(extension) && !extension.StartsWith(".", StringComparison.OrdinalIgnoreCase))
        {
            extension = $".{extension}";
        }

        if (!string.IsNullOrWhiteSpace(extension) && NonOptimizableExtensions.Contains(extension))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(mimeType) && NonOptimizableMimeTypes.Contains(mimeType);
    }
}
