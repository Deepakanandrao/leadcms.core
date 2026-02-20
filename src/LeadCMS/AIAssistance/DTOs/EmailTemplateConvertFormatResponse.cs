// <copyright file="EmailTemplateConvertFormatResponse.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enums;

namespace LeadCMS.Core.AIAssistance.DTOs;

/// <summary>
/// Response DTO returned after converting an email template between formats.
/// </summary>
public class EmailTemplateConvertFormatResponse
{
    /// <summary>
    /// Gets or sets the converted body template content.
    /// </summary>
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resulting format after conversion.
    /// </summary>
    public EmailTemplateFormat Format { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the conversion was performed by AI.
    /// When false, the conversion was done programmatically (MJML to HTML compilation).
    /// </summary>
    public bool AiPowered { get; set; }
}
