// <copyright file="IEmailWithLogService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Interfaces;

public interface IEmailWithLogService
{
    Task SendAsync(string subject, string fromEmail, string fromName, string[] recipients, string body, List<AttachmentDto>? attachments, int templateId = 0, int contactId = 0, int campaignId = 0);

    Task SendToContactAsync(int contactId, string subject, string fromEmail, string fromName, string body, List<AttachmentDto>? attachments, int scheduleId = 0, int templateId = 0, int campaignId = 0);
}