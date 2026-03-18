// <copyright file="SequenceEnrollment.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

public enum SequenceEnrollmentStatus
{
    Active = 0,
    Completed = 1,
    Exited = 2,
}

public enum SequenceExitReason
{
    None = 0,
    Completed = 1,
    Failed = 2,
    Unsubscribed = 3,
    ReplyStopped = 4,
    ManuallyRemoved = 5,
    Archived = 6,
}

public enum SequenceEnrollmentSource
{
    Manual = 0,
    Api = 1,
    Segment = 2,
    Migration = 3,
}

[Table("sequence_enrollment")]
[Index(nameof(SequenceId), nameof(ContactId))]
[Index(nameof(SequenceId), nameof(Status))]
public class SequenceEnrollment : BaseEntityWithIdAndDates
{
    [Required]
    public int SequenceId { get; set; }

    [JsonIgnore]
    [ForeignKey("SequenceId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Sequence? Sequence { get; set; }

    [Required]
    public int ContactId { get; set; }

    [Searchable]
    [JsonIgnore]
    [ForeignKey("ContactId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Contact? Contact { get; set; }

    [Required]
    public SequenceEnrollmentStatus Status { get; set; } = SequenceEnrollmentStatus.Active;

    /// <summary>
    /// Gets or sets the ID of the last completed step.
    /// </summary>
    public int? LastCompletedStepId { get; set; }

    [JsonIgnore]
    [ForeignKey("LastCompletedStepId")]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual SequenceStep? LastCompletedStep { get; set; }

    /// <summary>
    /// Gets or sets when the contact entered the sequence.
    /// </summary>
    [Required]
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the enrollment completed (all steps delivered).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets when the enrollment was exited early.
    /// </summary>
    public DateTime? ExitedAt { get; set; }

    /// <summary>
    /// Gets or sets the reason the enrollment ended.
    /// </summary>
    public SequenceExitReason ExitReason { get; set; } = SequenceExitReason.None;

    /// <summary>
    /// Gets or sets how the contact was enrolled.
    /// </summary>
    [Required]
    public SequenceEnrollmentSource EnrollmentSource { get; set; }

    /// <summary>
    /// Gets or sets the reason the contact was enrolled (operator note, trigger description, segment name).
    /// </summary>
    public string? EnrollmentReason { get; set; }

    /// <summary>
    /// Gets or sets custom template arguments for this enrollment.
    /// Merged into the template arguments for every email sent in this enrollment.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public Dictionary<string, string>? TemplateArguments { get; set; }
}
