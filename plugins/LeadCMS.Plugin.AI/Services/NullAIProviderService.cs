// <copyright file="NullAIProviderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;

namespace LeadCMS.Plugin.AI.Services;

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
