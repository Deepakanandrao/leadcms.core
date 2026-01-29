// <copyright file="ITextGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;

namespace LeadCMS.Core.AIAssistance.Interfaces;

public interface ITextGenerationService
{
    Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request);
}
