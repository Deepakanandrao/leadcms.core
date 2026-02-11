// <copyright file="ContactEmailCommunicationDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.DTOs;

public enum EmailCommunicationStatsGroupBy
{
    Day = 0,
    Week = 1,
    Month = 2,
}

public class ContactEmailCommunicationListItemDto
{
    public int Id { get; set; }

    public int? ContactId { get; set; }

    public int? ScheduleId { get; set; }

    public int? TemplateId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Recipients { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string MessageId { get; set; } = string.Empty;

    public EmailStatus Status { get; set; }

    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Body { get; set; }
}

public class ContactEmailCommunicationDetailsDto
{
    public int Id { get; set; }

    public int? ContactId { get; set; }

    public int? ScheduleId { get; set; }

    public int? TemplateId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Recipients { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string? Body { get; set; }

    public string MessageId { get; set; } = string.Empty;

    public EmailStatus Status { get; set; }

    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class ContactEmailCommunicationStatsDto
{
    public int ContactId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public long TotalCount { get; set; }

    public long SentCount { get; set; }

    public long ReceivedCount { get; set; }

    public long NotSentCount { get; set; }

    public DateTime? FirstCommunicationAt { get; set; }

    public DateTime? LastCommunicationAt { get; set; }

    public List<ContactEmailCommunicationTimelinePointDto> Timeline { get; set; } = new();
}

public class ContactEmailCommunicationTimelinePointDto
{
    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public long TotalCount { get; set; }

    public long SentCount { get; set; }

    public long ReceivedCount { get; set; }

    public long NotSentCount { get; set; }
}
