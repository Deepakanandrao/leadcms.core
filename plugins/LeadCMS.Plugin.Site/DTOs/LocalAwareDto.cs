// <copyright file="LocalAwareDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Enums;
using LeadCMS.Models;

namespace LeadCMS.Plugin.Site.DTOs;

public class ClientLocaleAwareDto
{
    /// <summary>
    /// Gets or sets client-provided categorisation tags to merge into the contact.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets UTM acquisition parameters from the client.
    /// Stored as first-touch attribution on the contact (not overwritten on subsequent submissions).
    /// </summary>
    public Utms? Utms { get; set; }

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