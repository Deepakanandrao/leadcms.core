// <copyright file="LocalAwareDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Enums;

namespace LeadCMS.Plugin.Site.DTOs;

public class ClientLocaleAwareDto
{
    [Required]
    public int TimeZoneOffset { get; set; }

    /// <summary>
    /// Gets or sets the format of <see cref="TimeZoneOffset"/>.
    /// Defaults to <see cref="TimezoneFormat.JavaScript"/> (the value returned by
    /// <c>Date.getTimezoneOffset()</c>, where UTC+2 is −120).
    /// Set to <see cref="TimezoneFormat.Utc"/> when passing a standard UTC offset
    /// (where UTC+2 is +120).
    /// </summary>
    public TimezoneFormat TimezoneFormat { get; set; } = TimezoneFormat.JavaScript;

    [Required]
    public string Language { get; set; } = string.Empty;
}