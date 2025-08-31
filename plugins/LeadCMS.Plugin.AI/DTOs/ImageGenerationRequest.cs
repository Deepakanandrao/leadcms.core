// <copyright file="ImageGenerationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.AI.DTOs;

public class ImageGenerationRequest
{
    public string Prompt { get; set; } = string.Empty;

    public string Size { get; set; } = "1024x1024";

    public string Quality { get; set; } = "standard";

    public string Style { get; set; } = "vivid";
}
