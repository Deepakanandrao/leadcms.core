// <copyright file="NullAIProviderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Exceptions;
using LeadCMS.Core.AIAssistance.Interfaces;

namespace LeadCMS.Core.AIAssistance.Services;

public class NullAIProviderService : IAIProviderService
{
    public string ProviderName => "OpenAI";

    public Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request)
    {
        throw new AIProviderException(ProviderName, "OpenAI provider is not configured. Please set the API key.");
    }

    public Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request)
    {
        throw new AIProviderException(ProviderName, "OpenAI provider is not configured. Please set the API key.");
    }
}
