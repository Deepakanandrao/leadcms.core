// <copyright file="EmailTemplateConvertFormatRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Enums;

namespace LeadCMS.Core.AIAssistance.DTOs;

/// <summary>
/// Request DTO for converting an email template between HTML and MJML formats.
/// </summary>
public class EmailTemplateConvertFormatRequest
{
    /// <summary>
    /// Gets or sets the body template content to convert.
    /// </summary>
    [Required(ErrorMessage = "BodyTemplate is required")]
    [MinLength(1, ErrorMessage = "BodyTemplate cannot be empty")]
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current format of the body template.
    /// </summary>
    [Required]
    public EmailTemplateFormat CurrentFormat { get; set; }

    /// <summary>
    /// Gets or sets the target format to convert to.
    /// </summary>
    [Required]
    public EmailTemplateFormat TargetFormat { get; set; }
}
