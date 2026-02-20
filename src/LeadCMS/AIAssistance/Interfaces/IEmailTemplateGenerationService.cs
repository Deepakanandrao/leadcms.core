// <copyright file="IEmailTemplateGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.DTOs;

namespace LeadCMS.Core.AIAssistance.Interfaces;

public interface IEmailTemplateGenerationService
{
    Task<EmailTemplateDetailsDto> GenerateEmailTemplateAsync(EmailTemplateGenerationRequest request);

    Task<EmailTemplateDetailsDto> GenerateEmailTemplateEditAsync(EmailTemplateEditRequest request);

    /// <summary>
    /// Converts an email template body between HTML and MJML formats.
    /// MJML to HTML is done programmatically; HTML to MJML requires AI.
    /// </summary>
    /// <param name="request">The conversion request containing the body and target format.</param>
    /// <returns>The converted body template and metadata.</returns>
    Task<EmailTemplateConvertFormatResponse> ConvertFormatAsync(EmailTemplateConvertFormatRequest request);
}