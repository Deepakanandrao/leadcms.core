// <copyright file="ContentGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Core.AIAssistance.DTOs;

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

    /// <summary>
    /// Gets or sets the target word count for the generated body content.
    /// If both WordCount and CharacterCount are specified, CharacterCount takes priority.
    /// </summary>
    public int? WordCount { get; set; }

    /// <summary>
    /// Gets or sets the target character count for the generated body content.
    /// Takes priority over WordCount if both are specified.
    /// </summary>
    public int? CharacterCount { get; set; }

    /// <summary>
    /// Gets or sets required media URLs from the media library that must be used in the article body.
    /// Format: "/api/media/{scopeUid}/{fileName}" or "scopeUid/fileName".
    /// </summary>
    [MaxLength(10, ErrorMessage = "Maximum of 10 required media items allowed")]
    public List<string>? RequiredMediaPaths { get; set; }
}
