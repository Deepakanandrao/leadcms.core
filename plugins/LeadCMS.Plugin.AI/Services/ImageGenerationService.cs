// <copyright file="ImageGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace LeadCMS.Plugin.AI.Services;

public class ImageGenerationService : IImageGenerationService
{
    private readonly IAIProviderService? provider;

    public ImageGenerationService(IConfiguration configuration)
    {
        provider = InitializeProvider();
    }

    public async Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request)
    {
        if (provider == null)
        {
            throw new AIProviderException("OpenAI", "OpenAI provider is not configured. Please set the API key.");
        }

        try
        {
            var response = await provider.GenerateImageAsync(request);
            Log.Information("Successfully generated image using OpenAI");
            return response;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate image using OpenAI");

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
                Log.Information("OpenAI provider initialized successfully for image generation");
                return openAIProvider;
            }
            else
            {
                Log.Warning("OpenAI API key not configured. Image generation will not be available.");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing OpenAI provider for image generation");
            return null;
        }
    }
}