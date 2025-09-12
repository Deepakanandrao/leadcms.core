// <copyright file="IEmailTemplateGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Plugin.AI.DTOs;

namespace LeadCMS.Plugin.AI.Interfaces;

public interface IEmailTemplateGenerationService
{
    Task<EmailTemplateDetailsDto> GenerateEmailTemplateAsync(EmailTemplateGenerationRequest request);

    Task<EmailTemplateDetailsDto> GenerateEmailTemplateEditAsync(EmailTemplateEditRequest request);
}