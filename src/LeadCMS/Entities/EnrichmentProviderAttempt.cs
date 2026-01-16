// <copyright file="EnrichmentProviderAttempt.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

/// <summary>
/// Categorical error reasons for enrichment attempts.
/// </summary>
public enum EnrichmentErrorCategory
{
    None,
    BadInput,
    AuthInvalid,
    AuthMissing,
    RateLimited,
    ProviderUnavailable,
    DataConflict,
    Blocked,
    Unknown,
}

/// <summary>
/// Individual execution attempt for a work item.
/// </summary>
[Table("enrichment_provider_attempt")]
[Index(nameof(ProviderKey), nameof(EntityType), nameof(EntityId), nameof(CreatedAt))]
public class EnrichmentProviderAttempt : BaseEntityWithId, IHasCreatedAt
{
    [Required]
    public int WorkItemId { get; set; }

    [ForeignKey("WorkItemId")]
    public virtual EnrichmentWorkItem? WorkItem { get; set; }

    [Required]
    [MaxLength(200)]
    public string ProviderKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public int EntityId { get; set; }

    public bool Success { get; set; }

    public EnrichmentErrorCategory ErrorCategory { get; set; } = EnrichmentErrorCategory.None;

    public string? ErrorMessage { get; set; }

    public int DurationMs { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ResponsePayload { get; set; }

    [Column(TypeName = "jsonb")]
    public string? RequestPayload { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
