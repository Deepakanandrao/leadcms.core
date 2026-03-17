// <copyright file="SequenceStep.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

public enum SequenceStepType
{
    Email = 0,
}

[Table("sequence_step")]
[Index(nameof(SequenceId), nameof(Position), IsUnique = true)]
[Index(nameof(SequenceId), nameof(Name), IsUnique = true)]
public class SequenceStep : BaseEntityWithIdAndDates
{
    [Required]
    public int SequenceId { get; set; }

    [JsonIgnore]
    [ForeignKey("SequenceId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Sequence? Sequence { get; set; }

    [Required]
    public int EmailTemplateId { get; set; }

    [JsonIgnore]
    [ForeignKey("EmailTemplateId")]
    public virtual EmailTemplate? EmailTemplate { get; set; }

    /// <summary>
    /// Gets or sets the execution order within the sequence.
    /// </summary>
    [Required]
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the step name.
    /// Used by runtime state and delivery tracking.
    /// </summary>
    [Required]
    [Searchable]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the step type. Phase 1 supports Email only.
    /// </summary>
    [Required]
    public SequenceStepType Type { get; set; } = SequenceStepType.Email;

    /// <summary>
    /// Gets or sets the timing configuration (delay, sendAt, allowedWeekDays).
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public SequenceStepTiming Timing { get; set; } = new();

    // Summary counters

    public int ScheduledCount { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }
}
