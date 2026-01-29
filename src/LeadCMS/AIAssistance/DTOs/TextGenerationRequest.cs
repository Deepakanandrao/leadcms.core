// <copyright file="TextGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Core.AIAssistance.DTOs;

public class TextGenerationRequest
{
    [Required(ErrorMessage = "User prompt is required")]
    [MinLength(1, ErrorMessage = "User prompt cannot be empty")]
    public string UserPrompt { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional image inputs to attach to the request for vision-capable models.
    /// </summary>
    public List<TextImageInput>? Images { get; set; }
}

public sealed class TextImageInput
{
    /// <summary>
    /// Gets or sets raw image bytes.
    /// </summary>
    [Required]
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets mIME type of the image (e.g., image/png, image/jpeg).
    /// </summary>
    [Required]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional file name for logging.
    /// </summary>
    public string? FileName { get; set; }
}
