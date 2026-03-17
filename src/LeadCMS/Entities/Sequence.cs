// <copyright file="Sequence.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

public enum SequenceStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Archived = 3,
}

[Table("sequence")]
[SupportsChangeLog]
[Index(nameof(Name), IsUnique = true)]
public class Sequence : BaseEntity
{
    [Required]
    [Searchable]
    public string Name { get; set; } = string.Empty;

    [Searchable]
    public string? Description { get; set; }

    [Required]
    public SequenceStatus Status { get; set; } = SequenceStatus.Draft;

    /// <summary>
    /// Gets or sets a value indicating whether to stop the sequence for a contact when they reply.
    /// </summary>
    public bool StopOnReply { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use each contact's individual timezone for send timing.
    /// When true, step timing is resolved in each contact's local timezone.
    /// Contacts without a timezone use the sequence-level <see cref="TimeZone"/> as fallback.
    /// </summary>
    public bool UseContactTimeZone { get; set; }

    /// <summary>
    /// Gets or sets the UTC offset in minutes for step timing (e.g. 120 for UTC+2, -300 for UTC-5).
    /// Used as fallback when <see cref="UseContactTimeZone"/> is false or when a contact has no timezone.
    /// </summary>
    public int TimeZone { get; set; }

    /// <summary>
    /// Gets or sets when the sequence was last activated.
    /// </summary>
    public DateTime? LastActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the sequence was last paused.
    /// </summary>
    public DateTime? LastPausedAt { get; set; }

    /// <summary>
    /// Gets or sets when the sequence was archived.
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    // Summary counters

    public int ActiveEnrollmentCount { get; set; }

    public int CompletedEnrollmentCount { get; set; }

    public int ExitedEnrollmentCount { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    // JSONB fields

    /// <summary>
    /// Gets or sets the enrollment configuration (modes, segment filtering, reentry policy).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public SequenceEnrollmentConfig? Enrollment { get; set; }

    /// <summary>
    /// Gets or sets optional UTM parameter overrides for this sequence.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public Utms? UtmParameters { get; set; }

    // Navigation

    [JsonIgnore]
    public virtual ICollection<SequenceStep>? Steps { get; set; }
}
