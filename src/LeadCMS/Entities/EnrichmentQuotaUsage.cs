// <copyright file="EnrichmentQuotaUsage.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

/// <summary>
/// Quota windows supported by the enrichment quota tracker.
/// </summary>
public enum EnrichmentQuotaWindow
{
    Daily,
    Monthly,
    Hourly,
}

/// <summary>
/// Quota window usage per provider.
/// </summary>
[Table("enrichment_quota_usage")]
[Index(nameof(ProviderKey), nameof(WindowType), nameof(WindowStart), IsUnique = true)]
public class EnrichmentQuotaUsage : BaseEntityWithId
{
    [Required]
    [MaxLength(200)]
    public string ProviderKey { get; set; } = string.Empty;

    [Required]
    public EnrichmentQuotaWindow WindowType { get; set; } = EnrichmentQuotaWindow.Daily;

    [Required]
    public DateTime WindowStart { get; set; }

    public int UsageCount { get; set; }
}
