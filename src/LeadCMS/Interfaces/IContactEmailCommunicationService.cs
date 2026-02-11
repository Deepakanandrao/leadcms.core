// <copyright file="IContactEmailCommunicationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Entities;

namespace LeadCMS.Interfaces;

public interface IContactEmailCommunicationService
{
    Task<QueryResult<EmailLog>> GetCommunicationsAsync(int contactId, string queryString, bool applyDefaultOrder = true);

    Task<EmailLog> GetCommunicationAsync(int contactId, int emailLogId);

    string? PrepareBody(EmailLog emailLog);

    Task<ContactEmailCommunicationStatsDto> GetStatsAsync(int contactId, DateTime? from, DateTime? to, EmailCommunicationStatsGroupBy groupBy);
}
