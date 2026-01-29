// <copyright file="IContentAITranslationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Core.AIAssistance.Interfaces;

public interface IContentAITranslationService
{
    /// <summary>
    /// Creates an AI-powered translation draft for the specified content.
    /// </summary>
    /// <param name="contentId">The ID of the content to translate.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <returns>The translated content draft.</returns>
    Task<ContentDetailsDto> CreateAITranslationDraftAsync(int contentId, string targetLanguage);
}
