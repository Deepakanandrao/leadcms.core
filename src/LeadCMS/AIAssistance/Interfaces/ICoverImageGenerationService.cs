// <copyright file="ICoverImageGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.DTOs;

namespace LeadCMS.Core.AIAssistance.Interfaces;

public interface ICoverImageGenerationService
{
    /// <summary>
    /// Generates a cover image for content based on title, description, and optional reference images.
    /// The generated image is saved to media storage.
    /// </summary>
    /// <param name="request">The cover image generation request.</param>
    /// <returns>Response containing the saved media information.</returns>
    Task<MediaDetailsDto> GenerateCoverImageAsync(CoverImageGenerationRequest request);

    /// <summary>
    /// Edits an existing cover image for content based on a prompt.
    /// The edited image is saved back to the existing media item.
    /// </summary>
    /// <param name="request">The cover image edit request.</param>
    /// <returns>Response containing the updated media information.</returns>
    Task<MediaDetailsDto> EditCoverImageAsync(CoverImageEditRequest request);
}
