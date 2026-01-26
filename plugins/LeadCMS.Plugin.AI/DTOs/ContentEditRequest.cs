// <copyright file="ContentEditRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.DTOs;

namespace LeadCMS.Plugin.AI.DTOs;

/// <summary>
/// Request DTO for AI-powered content editing that includes the current content data and user's editing prompt.
/// </summary>
public class ContentEditRequest : ContentUpdateDto
{
    /// <summary>
    /// Gets or sets the user's prompt describing the desired changes to the content.
    /// </summary>
    [Required(ErrorMessage = "Prompt is required")]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target word count for the edited body content.
    /// If both WordCount and CharacterCount are specified, CharacterCount takes priority.
    /// </summary>
    public int? WordCount { get; set; }

    /// <summary>
    /// Gets or sets the target character count for the edited body content.
    /// Takes priority over WordCount if both are specified.
    /// </summary>
    public int? CharacterCount { get; set; }
}
