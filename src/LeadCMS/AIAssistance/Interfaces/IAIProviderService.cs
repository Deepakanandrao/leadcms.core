// <copyright file="IAIProviderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;

namespace LeadCMS.Core.AIAssistance.Interfaces;

public interface IAIProviderService
{
    string ProviderName { get; }

    Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request);

    Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request);
}
