// <copyright file="ImageGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.AI.DTOs;

public class ImageGenerationRequest
{
    [Required(ErrorMessage = "Prompt is required")]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;

    public string Size { get; set; } = "1024x1024";

    public string Quality { get; set; } = "standard";

    public string Style { get; set; } = "vivid";
}
