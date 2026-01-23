// <copyright file="ContentGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.AI.DTOs;

public class ContentGenerationRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Language cannot be empty")]
    public string Language { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "ContentType cannot be empty")]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;

    public int? ReferenceContentId { get; set; }
}
