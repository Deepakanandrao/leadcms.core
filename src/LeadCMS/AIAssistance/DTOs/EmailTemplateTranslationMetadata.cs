// <copyright file="EmailTemplateTranslationMetadata.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Enums;

namespace LeadCMS.Core.AIAssistance.DTOs;

public class EmailTemplateTranslationMetadata
{
    public string Name { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string BodyTemplate { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;
}