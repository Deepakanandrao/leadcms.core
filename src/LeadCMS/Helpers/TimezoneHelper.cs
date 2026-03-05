// <copyright file="TimezoneHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enums;

namespace LeadCMS.Helpers;

/// <summary>
/// Utility methods for normalizing timezone offset values.
/// The internal canonical format is the standard UTC offset in minutes
/// (positive for east of UTC, negative for west).
/// </summary>
public static class TimezoneHelper
{
    /// <summary>
    /// Converts a raw timezone offset to the canonical UTC-offset-minutes format.
    /// </summary>
    /// <param name="rawOffset">The raw offset value supplied by the client.</param>
    /// <param name="format">The convention the client used to produce <paramref name="rawOffset"/>.</param>
    /// <returns>The offset expressed as standard UTC-offset minutes (e.g. +120 for UTC+2).</returns>
    public static int NormalizeToUtcOffset(int rawOffset, TimezoneFormat format)
    {
        return format switch
        {
            TimezoneFormat.JavaScript => -rawOffset,
            TimezoneFormat.Utc => rawOffset,
            _ => -rawOffset, // default to JS convention
        };
    }

    /// <summary>
    /// Converts a nullable raw timezone offset to the canonical UTC-offset-minutes format.
    /// Returns <c>null</c> when <paramref name="rawOffset"/> is <c>null</c>.
    /// </summary>
    public static int? NormalizeToUtcOffset(int? rawOffset, TimezoneFormat format)
    {
        return rawOffset.HasValue ? NormalizeToUtcOffset(rawOffset.Value, format) : null;
    }
}
