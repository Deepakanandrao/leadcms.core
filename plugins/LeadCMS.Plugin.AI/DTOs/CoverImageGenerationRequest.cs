// <copyright file="CoverImageGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Plugin.AI.DTOs;

public class CoverImageGenerationRequest
{
    /// <summary>
    /// Gets or sets the title of the content to generate a cover image for (mandatory).
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [MinLength(1, ErrorMessage = "Title cannot be empty")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the content to generate a cover image for (mandatory).
    /// </summary>
    [Required(ErrorMessage = "Description is required")]
    [MinLength(1, ErrorMessage = "Description cannot be empty")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the slug of the content, used as the scope for storing the generated image (mandatory).
    /// </summary>
    [Required(ErrorMessage = "Slug is required")]
    [MinLength(1, ErrorMessage = "Slug cannot be empty")]
    public string Slug { get; set; } = string.Empty;

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
