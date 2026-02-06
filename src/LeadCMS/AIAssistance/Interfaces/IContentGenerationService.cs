// <copyright file="IContentGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.DTOs;

namespace LeadCMS.Core.AIAssistance.Interfaces;

public interface IContentGenerationService
{
    Task<ContentCreateDto> GenerateContentAsync(ContentGenerationRequest request);

    Task<ContentCreateDto> GenerateContentEditAsync(ContentEditRequest request);
}
