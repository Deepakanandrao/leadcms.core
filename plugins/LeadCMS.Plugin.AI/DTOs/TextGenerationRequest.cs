// <copyright file="TextGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.AI.DTOs;

public class TextGenerationRequest
{
    public string UserPrompt { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;
}
