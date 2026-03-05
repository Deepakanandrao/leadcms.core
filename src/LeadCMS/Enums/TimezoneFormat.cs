// <copyright file="TimezoneFormat.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enums;

/// <summary>
/// Identifies the convention used for a timezone offset value.
/// </summary>
public enum TimezoneFormat
{
    /// <summary>
    /// JavaScript <c>Date.getTimezoneOffset()</c> convention.
    /// The sign is inverted relative to the standard UTC offset:
    /// UTC+2 → −120, UTC−5 → +300.
    /// </summary>
    JavaScript = 0,

    /// <summary>
    /// Standard UTC offset in minutes.
    /// UTC+2 → +120, UTC−5 → −300.
    /// </summary>
    Utc = 1,
}
