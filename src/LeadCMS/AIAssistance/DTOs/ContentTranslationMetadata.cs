// <copyright file="ContentTranslationMetadata.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Core.AIAssistance.DTOs;

public class ContentTranslationMetadata
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? CoverImageAlt { get; set; }

    public string[] Tags { get; set; } = Array.Empty<string>();
}
