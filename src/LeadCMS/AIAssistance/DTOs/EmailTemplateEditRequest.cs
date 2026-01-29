// <copyright file="EmailTemplateEditRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.DTOs;

namespace LeadCMS.Core.AIAssistance.DTOs;

/// <summary>
/// Request DTO for AI-powered email template editing that includes the current email template data and user's editing prompt.
/// </summary>
public class EmailTemplateEditRequest : EmailTemplateUpdateDto
{
    /// <summary>
    /// Gets or sets the user's prompt describing the desired changes to the email template.
    /// </summary>
    [Required(ErrorMessage = "Prompt is required")]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;
}