// <copyright file="CoverImageEditRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Core.AIAssistance.DTOs;

/// <summary>
/// Request DTO for AI-powered cover image editing using an existing cover image.
/// </summary>
public class CoverImageEditRequest
{
    /// <summary>
    /// Gets or sets the current cover image URL (mandatory).
    /// Must start with "/api/media/".
    /// </summary>
    [Required(ErrorMessage = "CoverImageUrl is required")]
    [MinLength(1, ErrorMessage = "CoverImageUrl cannot be empty")]
    public string CoverImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the related content (mandatory).
    /// </summary>
    [Required(ErrorMessage = "ContentTitle is required")]
    [MinLength(1, ErrorMessage = "ContentTitle cannot be empty")]
    public string ContentTitle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the related content (mandatory).
    /// </summary>
    [Required(ErrorMessage = "ContentDescription is required")]
    [MinLength(1, ErrorMessage = "ContentDescription cannot be empty")]
    public string ContentDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's prompt describing the desired edits to the cover image.
    /// </summary>
    [Required(ErrorMessage = "Prompt is required")]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional sample image URLs from the media library (up to 5).
    /// These images are used strictly as style references.
    /// Format: "/api/media/{scopeUid}/{fileName}" or "scopeUid/fileName".
    /// </summary>
    [MaxLength(5, ErrorMessage = "Maximum of 5 sample images allowed")]
    public List<string>? SampleImagePaths { get; set; }
}
