// <copyright file="SequenceDeliveryDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Entities;

namespace LeadCMS.Core.Sequences.DTOs;

public class SequenceDeliveryDetailsDto
{
    public int Id { get; set; }

    public int SequenceId { get; set; }

    public int SequenceEnrollmentId { get; set; }

    public int SequenceStepId { get; set; }

    public int ContactId { get; set; }

    public ContactDetailsDto? Contact { get; set; }

    public SequenceDeliveryStatus Status { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime? SentAt { get; set; }

    public string? SkipReason { get; set; }

    public string? ErrorMessage { get; set; }

    public int? EmailLogId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
