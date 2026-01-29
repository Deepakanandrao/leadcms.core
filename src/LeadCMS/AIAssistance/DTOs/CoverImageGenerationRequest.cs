// <copyright file="CoverImageGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Core.AIAssistance.DTOs;

public class CoverImageGenerationRequest
{
    /// <summary>
    /// Gets or sets the title of the related content to generate a cover image for (mandatory).
    /// </summary>
    [Required(ErrorMessage = "ContentTitle is required")]
    [MinLength(1, ErrorMessage = "ContentTitle cannot be empty")]
    public string ContentTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the related content to generate a cover image for (mandatory).
    /// </summary>
    [Required(ErrorMessage = "ContentDescription is required")]
    [MinLength(1, ErrorMessage = "ContentDescription cannot be empty")]
    public string ContentDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the slug of the related content, used as the scope for storing the generated image (mandatory).
    /// </summary>
    [Required(ErrorMessage = "ContentSlug is required")]
    [MinLength(1, ErrorMessage = "ContentSlug cannot be empty")]
    public string ContentSlug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional prompt with specific ideas for the cover image.
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    /// Gets or sets optional sample image URLs from the media library (up to 5).
    /// Format: "scopeUid/fileName" (e.g., "my-post/cover.png").
    /// If not provided, the system will automatically find recent cover images.
    /// </summary>
    [MaxLength(5, ErrorMessage = "Maximum of 5 sample images allowed")]
    public List<string>? SampleImagePaths { get; set; }
}
