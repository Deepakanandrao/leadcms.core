// <copyright file="SequenceEnrollmentDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Models;

namespace LeadCMS.Core.Sequences.DTOs;

public class SequenceEnrollmentCreateDto
{
    [Required]
    public int[] ContactIds { get; set; } = Array.Empty<int>();

    public string? EnrollmentReason { get; set; }

    /// <summary>
    /// Gets or sets custom template arguments applied to every email sent in this enrollment.
    /// </summary>
    public Dictionary<string, string>? TemplateArguments { get; set; }
}

public class SequenceEnrollmentStopDto
{
    [Required]
    public int[] EnrollmentIds { get; set; } = Array.Empty<int>();
}

public class SequenceEnrollmentDetailsDto
{
    public int Id { get; set; }

    public int SequenceId { get; set; }

    public int ContactId { get; set; }

    public ContactDetailsDto? Contact { get; set; }

    public SequenceEnrollmentStatus Status { get; set; }

    public int? LastCompletedStepId { get; set; }

    public SequenceStepDetailsDto? LastCompletedStep { get; set; }

    public DateTime EnteredAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExitedAt { get; set; }

    public SequenceExitReason ExitReason { get; set; }

    public SequenceEnrollmentSource EnrollmentSource { get; set; }

    public string? EnrollmentReason { get; set; }

    public Dictionary<string, string>? TemplateArguments { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the step-by-step timeline for this enrollment.
    /// Populated by the GetOne endpoint; null on list responses.
    /// </summary>
    public List<EnrollmentStepTimelineEntryDto>? Steps { get; set; }
}

public enum EnrollmentStepTimelineStatus
{
    Sent = 0,
    Scheduled = 1,
    Planned = 2,
    Failed = 3,
    Skipped = 4,
}

public class EnrollmentStepTimelineEntryDto
{
    public int StepId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Position { get; set; }

    public int EmailTemplateId { get; set; }

    public SequenceStepTiming Timing { get; set; } = new();

    public EnrollmentStepTimelineStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the delivery was sent (for Sent steps).
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Gets or sets when the delivery is/was scheduled (for Sent and Scheduled steps from actual deliveries, estimated for Planned steps).
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Gets or sets the delivery ID if a delivery record exists.
    /// </summary>
    public int? DeliveryId { get; set; }

    /// <summary>
    /// Gets or sets the email log ID if the delivery was sent.
    /// </summary>
    public int? EmailLogId { get; set; }

    /// <summary>
    /// Gets or sets the skip reason if the step was skipped.
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Gets or sets the error message if the step failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the email preview for this step.
    /// For sent steps, sourced from the actual EmailLog.
    /// For scheduled/planned steps, rendered from the email template with the contact's data.
    /// </summary>
    public StepEmailPreviewDto? EmailPreview { get; set; }
}

public class StepEmailPreviewDto
{
    public string Subject { get; set; } = string.Empty;

    public string? Body { get; set; }

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;
}
