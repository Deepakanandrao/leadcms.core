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

    /// <summary>
    /// Gets or sets the ID of an existing email template to use as a visual and structural
    /// reference when the AI edits the template. Useful for guiding tone, layout, or styling.
    /// </summary>
    public int? ReferenceEmailTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template variables (variable names with descriptions)
    /// that the AI should be aware of and can utilise in the generated template.
    /// Keys are variable names (e.g. "orderNumber"), values are descriptions of the variable.
    /// </summary>
    public Dictionary<string, string>? TemplateVariables { get; set; }
}