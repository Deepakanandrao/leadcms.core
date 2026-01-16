// <copyright file="EnrichmentProviderConfig.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeadCMS.Entities;

/// <summary>
/// Provider-level configuration and limits stored in the core schema.
/// </summary>
[Table("enrichment_provider_config")]
public class EnrichmentProviderConfig : IHasCreatedAt, IHasUpdatedAt
{
    [Key]
    [MaxLength(200)]
    public string ProviderKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = false;

    [Column(TypeName = "jsonb")]
    public string? Configuration { get; set; }

    public int? DailyQuota { get; set; }

    public int? MonthlyQuota { get; set; }

    public int? HourlyQuota { get; set; }

    public int? MinCallIntervalMs { get; set; }

    public int? MaxConcurrency { get; set; }

    public bool? AllowParallelCalls { get; set; }

    public DateTime? LastConfigChangeAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<EnrichmentWorkItem>? WorkItems { get; set; }
}
