// <copyright file="ImageGenerationResponse.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Core.AIAssistance.DTOs;

public class ImageGenerationResponse
{
    public List<GeneratedImage> Images { get; set; } = new List<GeneratedImage>();

    public string Model { get; set; } = string.Empty;

    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public class GeneratedImage
{
    public string Url { get; set; } = string.Empty;

    public byte[]? ImageData { get; set; }

    public string? RevisedPrompt { get; set; }
}
