// <copyright file="EnrichmentWorkItem.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

/// <summary>
/// Supported triggers for enrichment scheduling.
/// </summary>
public enum EnrichmentTrigger
{
    Created,
    Updated,
    Manual,
    ProviderEnabled,
}

/// <summary>
/// Lifecycle states for enrichment work items.
/// </summary>
public enum EnrichmentWorkItemStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Blocked,
    Cancelled,
}

/// <summary>
/// Durable work queue item produced by the scheduler task.
/// </summary>
[Table("enrichment_work_item")]
[Index(nameof(Status))]
public class EnrichmentWorkItem : BaseEntityWithIdAndDates
{
    [Required]
    [MaxLength(200)]
    public string ProviderKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public int EntityId { get; set; }

    [Required]
    public EnrichmentTrigger Trigger { get; set; } = EnrichmentTrigger.Created;

    [Required]
    public EnrichmentWorkItemStatus Status { get; set; } = EnrichmentWorkItemStatus.Pending;

    public int RetryCount { get; set; }

    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExecutedAt { get; set; }

    [ForeignKey("ProviderKey")]
    public virtual EnrichmentProviderConfig? ProviderConfig { get; set; }

    public virtual ICollection<EnrichmentProviderAttempt>? Attempts { get; set; }
}
