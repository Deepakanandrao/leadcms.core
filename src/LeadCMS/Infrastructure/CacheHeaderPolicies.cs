// <copyright file="CacheHeaderPolicies.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;

namespace LeadCMS.Infrastructure;

/// <summary>
/// Centralizes HTTP cache policies for static and media assets.
/// </summary>
internal static partial class CacheHeaderPolicies
{
    public const string NoCacheMustRevalidate = "no-cache, must-revalidate";
    public const string LongTermImmutable = "public, max-age=31536000, immutable";
    public const string DefaultMediaCache = "public, max-age=1200";

    public static string GetStaticAssetCacheControl(string? fileName, string? requestPath)
    {
        if (string.Equals(fileName, "index.html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestPath, "/index.html", StringComparison.OrdinalIgnoreCase))
        {
            return NoCacheMustRevalidate;
        }

        return IsFingerprintedStaticAsset(fileName)
            ? LongTermImmutable
            : NoCacheMustRevalidate;
    }

    public static string GetMediaCacheControl(string? cacheBustVersion)
    {
        return string.IsNullOrWhiteSpace(cacheBustVersion)
            ? DefaultMediaCache
            : LongTermImmutable;
    }

    private static bool IsFingerprintedStaticAsset(string? fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) && FingerprintedStaticAssetRegex().IsMatch(fileName);
    }

    [GeneratedRegex(@"\.[0-9a-f]{8,}\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FingerprintedStaticAssetRegex();
}