// <copyright file="ContentEditRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.AI.DTOs;

public class ContentEditRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "ContentId must be greater than 0")]
    public int ContentId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;
}
