// <copyright file="CampaignDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class CampaignCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int EmailTemplateId { get; set; }

    [Required]
    public int[] SegmentIds { get; set; } = Array.Empty<int>();

    public int[]? ExcludeSegmentIds { get; set; }

    /// <summary>
    /// Gets or sets the scheduled send time. Interpreted in the campaign's TimeZone.
    /// Null means send immediately when launched.
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
    /// When not provided, defaults to the system default language.
    /// </summary>
    public string? Language { get; set; }
}

public class CampaignUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [MinLength(1)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public int? EmailTemplateId { get; set; }

    public int[]? SegmentIds { get; set; }

    public int[]? ExcludeSegmentIds { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public int? TimeZone { get; set; }

    public bool? UseContactTimeZone { get; set; }

    public string? Language { get; set; }
}

public class CampaignDetailsDto : CampaignCreateDto
{
    public int Id { get; set; }

    public CampaignStatus Status { get; set; }

    public DateTime? SendStartedAt { get; set; }

    public DateTime? SendCompletedAt { get; set; }

    public int TotalRecipients { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Ignore]
    public EmailTemplateDetailsDto? EmailTemplate { get; set; }
}

public class CampaignStatisticsDto
{
    public int TotalRecipients { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }

    public int PendingCount { get; set; }

    public int SkippedUnsubscribed { get; set; }

    public int SkippedDuplicate { get; set; }

    public int SkippedSuppressed { get; set; }

    public int SkippedInvalidEmail { get; set; }
}

public class CampaignLaunchDto
{
    /// <summary>
    /// Gets or sets a value indicating whether to send immediately or use the campaign's scheduled time.
    /// </summary>
    public bool SendNow { get; set; } = true;

    /// <summary>
    /// Gets or sets the scheduled send time. Interpreted in the provided TimeZone.
    /// Only used if SendNow is false.
    /// </summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC offset in minutes for the scheduled send time (e.g. 120 for UTC+2, -300 for UTC-5).
    /// Used when UseContactTimeZone is false.
    /// </summary>
    public int? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to send at ScheduledAt in each contact's individual timezone.
    /// When true, each contact receives the email when ScheduledAt arrives in their timezone.
    /// Contacts without a timezone use the campaign's TimeZone as a fallback.
    /// </summary>
    public bool UseContactTimeZone { get; set; }
}

public class CampaignRecipientDetailsDto
{
    public int Id { get; set; }

    public int CampaignId { get; set; }

    public int ContactId { get; set; }

    [Ignore]
    public ContactDetailsDto? Contact { get; set; }

    public CampaignRecipientStatus Status { get; set; }

    public CampaignSkipReason SkipReason { get; set; }

    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Gets or sets when this recipient is expected to be sent the email (UTC).
    /// For already sent recipients, this is the actual <see cref="SentAt"/> timestamp.
    /// </summary>
    public DateTime? ExpectedSendAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class CampaignPreviewRequestDto
{
    /// <summary>
    /// Gets or sets the email template ID to use for the preview.
    /// </summary>
    [Required]
    public int EmailTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the segment IDs that form the campaign audience.
    /// </summary>
    [Required]
    public int[] SegmentIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets the segment IDs to exclude from the campaign audience.
    /// </summary>
    public int[]? ExcludeSegmentIds { get; set; }

    /// <summary>
    /// Gets or sets the language for template matching. Defaults to system default language if not provided.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the specific contact ID to use for rendering the template preview.
    /// When not provided, a contact from the target audience is used.
    /// </summary>
    public int? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the type of dummy contact to generate for the preview.
    /// Ignored when <see cref="ContactId"/> is provided. Defaults to <see cref="PreviewContactType.Full"/>.
    /// </summary>
    public PreviewContactType? ContactType { get; set; }

    /// <summary>
    /// Gets or sets custom template parameters provided by client code.
    /// These values are merged on top of built-in contact template arguments.
    /// </summary>
    public Dictionary<string, JsonElement>? CustomTemplateParameters { get; set; }
}

public class CampaignPreviewResultDto
{
    /// <summary>
    /// Gets or sets the total number of contacts in the target audience (after deduplication and exclusion).
    /// </summary>
    public int TotalAudienceCount { get; set; }

    /// <summary>
    /// Gets or sets the number of contacts that would actually receive the email (valid email, not unsubscribed).
    /// </summary>
    public int SendableCount { get; set; }

    /// <summary>
    /// Gets or sets the number of contacts that would be skipped due to unsubscription.
    /// </summary>
    public int UnsubscribedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of contacts that would be skipped due to invalid/missing email.
    /// </summary>
    public int InvalidEmailCount { get; set; }

    /// <summary>
    /// Gets or sets the template preview result with rendered email content.
    /// </summary>
    public EmailTemplatePreviewResultDto TemplatePreview { get; set; } = new();
}
