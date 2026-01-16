// <copyright file="EnrichmentAudit.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

/// <summary>
/// Field-level audit of enrichment writes.
/// </summary>
[Table("enrichment_audit")]
[Index(nameof(EntityType), nameof(EntityId), nameof(FieldName))]
public class EnrichmentAudit : BaseEntityWithId
{
    [Required]
    [MaxLength(128)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    public int EntityId { get; set; }

    [Required]
    [MaxLength(128)]
    public string FieldName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ProviderKey { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public double? Confidence { get; set; }

    public DateTime EnrichedAt { get; set; } = DateTime.UtcNow;
}
