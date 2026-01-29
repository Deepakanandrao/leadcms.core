// <copyright file="ImageGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Core.AIAssistance.DTOs;

public class ImageGenerationRequest
{
    [Required(ErrorMessage = "Prompt is required")]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;

    public string Quality { get; set; } = "Auto";

    public string Style { get; set; } = "Auto";

    public int? Width { get; set; }

    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets the primary image to edit, when performing image edits.
    /// </summary>
    public ImageInput? EditImage { get; set; }

    /// <summary>
    /// Gets or sets optional reference images used for style guidance.
    /// </summary>
    public List<ImageInput>? SampleImages { get; set; }
}

public sealed class ImageInput
{
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public string FileName { get; set; } = "image.png";

    public string? MimeType { get; set; }
}
