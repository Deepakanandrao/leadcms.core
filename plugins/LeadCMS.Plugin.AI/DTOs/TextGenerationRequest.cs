// <copyright file="TextGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.AI.DTOs;

public class TextGenerationRequest
{
    [Required(ErrorMessage = "User prompt is required")]
    [MinLength(1, ErrorMessage = "User prompt cannot be empty")]
    public string UserPrompt { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;
}
