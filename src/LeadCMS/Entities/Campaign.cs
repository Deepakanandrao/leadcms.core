// <copyright file="Campaign.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

public enum CampaignStatus
{
    Draft = 0,
    Scheduled = 1,
    Sending = 2,
    Sent = 3,
    Cancelled = 4,
    Paused = 5,
}

[Table("campaign")]
[SupportsChangeLog]
[Index(nameof(Name), IsUnique = true)]
public class Campaign : BaseEntity
{
    [Required]
    [Searchable]
    public string Name { get; set; } = string.Empty;

    [Searchable]
    public string? Description { get; set; }

    [Required]
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    /// <summary>
    /// Gets or sets the template to send.
    /// </summary>
    [Required]
    public int EmailTemplateId { get; set; }

    [JsonIgnore]
    [ForeignKey("EmailTemplateId")]
    public virtual EmailTemplate? EmailTemplate { get; set; }

    /// <summary>
    /// Gets or sets the included segment IDs (audience).
    /// </summary>
    [Required]
    [Column(TypeName = "integer[]")]
    public int[] SegmentIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the excluded segment IDs.
    /// </summary>
    [Column(TypeName = "integer[]")]
    public int[]? ExcludeSegmentIds { get; set; }

    /// <summary>
    /// Gets or sets the scheduled send time. Interpreted in the campaign's TimeZone.
    /// Null means send immediately.
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC offset in minutes for the scheduled send time (e.g. 120 for UTC+2, -300 for UTC-5).
    /// Used when UseContactTimeZone is false. When set, ScheduledAt is interpreted in this offset.
    /// </summary>
    public int? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to send at ScheduledAt in each contact's individual timezone.
    /// When true, each contact receives the email when ScheduledAt arrives in their timezone.
    /// Contacts without a timezone use the campaign's TimeZone as a fallback (defaults to 0 / UTC).
    /// </summary>
    public bool UseContactTimeZone { get; set; }

    /// <summary>
    /// Gets or sets the language for the campaign. The email template matching this language will be used.
    /// Defaults to the system default language.
    /// </summary>
    [Required]
    public string Language { get; set; } = LanguageHelper.DefaultFallbackLanguage;

    /// <summary>
    /// Gets or sets when the campaign actually started sending.
    /// </summary>
    public DateTime? SendStartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the campaign finished sending.
    /// </summary>
    public DateTime? SendCompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the total recipient count after audience resolution.
    /// </summary>
    public int TotalRecipients { get; set; }

    /// <summary>
    /// Gets or sets the count of successfully sent emails.
    /// </summary>
    public int SentCount { get; set; }

    /// <summary>
    /// Gets or sets the count of failed sends.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the count of skipped sends (unsubscribed, duplicate, etc.).
    /// </summary>
    public int SkippedCount { get; set; }
}
