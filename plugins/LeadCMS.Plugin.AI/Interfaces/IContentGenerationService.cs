// <copyright file="IContentGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Plugin.AI.DTOs;

namespace LeadCMS.Plugin.AI.Interfaces;

public interface IContentGenerationService
{
    Task<ContentDetailsDto> GenerateContentAsync(ContentGenerationRequest request);

    Task<ContentDetailsDto> GenerateContentEditAsync(ContentEditRequest request);
}
