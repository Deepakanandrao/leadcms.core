// <copyright file="IImageGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.AI.DTOs;

namespace LeadCMS.Plugin.AI.Interfaces;

public interface IImageGenerationService
{
    Task<ImageGenerationResponse> GenerateImageAsync(ImageGenerationRequest request);
}
