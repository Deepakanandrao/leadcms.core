// <copyright file="IEmailTemplateAITranslationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Core.AIAssistance.Interfaces;

public interface IEmailTemplateAITranslationService
{
    /// <summary>
    /// Creates an AI-powered translation draft for the specified email template.
    /// </summary>
    /// <param name="emailTemplateId">The ID of the email template to translate.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <param name="targetEmailGroupId">Optional ID of the target email group for the translation. If not provided, uses the same group as the original template.</param>
    /// <returns>The translated email template draft.</returns>
    Task<EmailTemplateDetailsDto> CreateAITranslationDraftAsync(int emailTemplateId, string targetLanguage, int? targetEmailGroupId = null);
}