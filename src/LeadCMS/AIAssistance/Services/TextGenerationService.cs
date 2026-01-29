// <copyright file="TextGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Exceptions;
using LeadCMS.Core.AIAssistance.Interfaces;
using Serilog;

namespace LeadCMS.Core.AIAssistance.Services;

public class TextGenerationService : ITextGenerationService
{
    private readonly IAIProviderService provider;

    public TextGenerationService(IAIProviderService provider)
    {
        this.provider = provider;
    }

    public async Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request)
    {
        try
        {
            Log.Debug(
                "Starting text generation - SystemPrompt: {SystemPromptLength} chars, UserPrompt: {UserPromptLength} chars",
                request.SystemPrompt?.Length ?? 0,
                request.UserPrompt?.Length ?? 0);

            var response = await provider.GenerateTextAsync(request);

            Log.Information(
                "Text generation completed - Model: {Model}, TotalTokens: {TokensUsed}, OutputLength: {OutputLength} chars, FinishReason: {FinishReason}",
                response.Model,
                response.TokensUsed,
                response.GeneratedText?.Length ?? 0,
                response.FinishReason);

            return response;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate text using OpenAI");

            // Re-throw inner exception if it exists, otherwise throw the current exception
            if (ex.InnerException != null)
            {
                throw ex.InnerException;
            }

            throw;
        }
    }
}
