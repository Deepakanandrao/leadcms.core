// <copyright file="TextGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace LeadCMS.Plugin.AI.Services;

public class TextGenerationService : ITextGenerationService
{
    private readonly IAIProviderService? provider;

    public TextGenerationService(IConfiguration configuration)
    {
        provider = InitializeProvider();
    }

    public async Task<TextGenerationResponse> GenerateTextAsync(TextGenerationRequest request)
    {
        if (provider == null)
        {
            throw new AIProviderException("OpenAI", "OpenAI provider is not configured. Please set the API key.");
        }

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

    private IAIProviderService? InitializeProvider()
    {
        try
        {
            // Initialize OpenAI provider if API key is configured
            if (!string.IsNullOrEmpty(AIPlugin.Configuration.OpenAI.ApiKey))
            {
                var openAIProvider = new OpenAIProviderService(AIPlugin.Configuration.OpenAI);
                Log.Information("OpenAI provider initialized successfully");
                return openAIProvider;
            }
            else
            {
                Log.Warning("OpenAI API key not configured. Text generation will not be available.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing OpenAI provider");
            return null;
        }
    }
}