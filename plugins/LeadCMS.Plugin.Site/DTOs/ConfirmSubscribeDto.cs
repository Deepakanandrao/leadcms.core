// <copyright file="ConfirmSubscribeDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.Site.DTOs;

public class ConfirmSubscribeDto
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
