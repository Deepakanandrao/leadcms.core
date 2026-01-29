// <copyright file="ImageGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Interfaces;
using Serilog;

namespace LeadCMS.Core.AIAssistance.Services;

public class ImageGenerationService : IImageGenerationService
{
    private readonly IAIProviderService provider;

    public ImageGenerationService(IAIProviderService provider)
    {
        this.provider = provider;
    }

    public async Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request)
    {
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
}

