// <copyright file="IEmailFromTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Interfaces;

public interface IEmailFromTemplateService
{
    Task SendAsync(string templateName, string language, string[] recipients, Dictionary<string, object>? templateArguments, List<AttachmentDto>? attachments, int contactId = 0, int campaignId = 0);

    Task SendToContactAsync(int contactId, string templateName, Dictionary<string, object>? templateArguments, List<AttachmentDto>? attachments, int scheduleId = 0, int campaignId = 0);
}