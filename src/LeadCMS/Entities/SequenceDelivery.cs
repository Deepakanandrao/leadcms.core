// <copyright file="SequenceDelivery.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

public enum SequenceDeliveryStatus
{
    Scheduled = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3,
}

[Table("sequence_delivery")]
[Index(nameof(SequenceEnrollmentId), nameof(SequenceStepId), IsUnique = true)]
[Index(nameof(Status), nameof(ScheduledAt))]
public class SequenceDelivery : BaseEntityWithIdAndDates
{
    [Required]
    public int SequenceId { get; set; }

    [JsonIgnore]
    [ForeignKey("SequenceId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Sequence? Sequence { get; set; }

    [Required]
    public int SequenceEnrollmentId { get; set; }

    [JsonIgnore]
    [ForeignKey("SequenceEnrollmentId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual SequenceEnrollment? SequenceEnrollment { get; set; }

    [Required]
    public int SequenceStepId { get; set; }

    [JsonIgnore]
    [ForeignKey("SequenceStepId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual SequenceStep? SequenceStep { get; set; }

    [Required]
    public int ContactId { get; set; }

    [JsonIgnore]
    [ForeignKey("ContactId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Contact? Contact { get; set; }

    [Required]
    public SequenceDeliveryStatus Status { get; set; } = SequenceDeliveryStatus.Scheduled;

    /// <summary>
    /// Gets or sets when the delivery is scheduled to be sent.
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Gets or sets when the email was actually sent.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Gets or sets the reason this delivery was skipped.
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Gets or sets the error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the optional FK to the email log entry for this delivery.
    /// </summary>
    public int? EmailLogId { get; set; }

    [JsonIgnore]
    [ForeignKey("EmailLogId")]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual EmailLog? EmailLog { get; set; }
}
