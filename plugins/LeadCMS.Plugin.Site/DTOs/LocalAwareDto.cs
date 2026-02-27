// <copyright file="LocalAwareDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.Site.DTOs;

public class ClientLocaleAwareDto
{
    [Required]
    public int TimeZoneOffset { get; set; }

    [Required]
    public string Language { get; set; } = string.Empty;
}