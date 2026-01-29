// <copyright file="TextGenerationResponse.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Core.AIAssistance.DTOs;

public class TextGenerationResponse
{
    public string GeneratedText { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int TokensUsed { get; set; }

    public string FinishReason { get; set; } = string.Empty;

    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}
