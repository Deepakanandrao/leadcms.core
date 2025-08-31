// <copyright file="ContentTranslationRequest.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.AI.DTOs;

public class ContentTranslationRequest
{
    public int ContentId { get; set; }

    public string TargetLanguage { get; set; } = string.Empty;
}
