// <copyright file="EmailTemplateGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Enums;

namespace LeadCMS.Core.AIAssistance.DTOs;

public class EmailTemplateGenerationRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Language cannot be empty")]
    public string Language { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "EmailGroupId must be greater than 0")]
    public int EmailGroupId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the email template to generate.
    /// Used to give the AI additional context about the expected visual style and purpose.
    /// When not specified, defaults to General (no additional category-specific guidance).
    /// </summary>
    public EmailTemplateCategory? Category { get; set; }

    /// <summary>
    /// Gets or sets the ID of an existing email template to use as a visual and structural
    /// reference when generating the new template. When provided, the referenced template is
    /// used as the sample instead of an automatically selected one from the group.
    /// </summary>
    public int? ReferenceEmailTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template variables (variable names with descriptions)
    /// that the AI should be aware of and can utilise in the generated template.
    /// Keys are variable names (e.g. "orderNumber"), values are descriptions of the variable.
    /// </summary>
    public Dictionary<string, string>? TemplateVariables { get; set; }
}