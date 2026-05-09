// <copyright file="Redirect.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LeadCMS.DataAnnotations;
using LeadCMS.Enums;

namespace LeadCMS.Entities;

[Table("redirect")]
[SupportsChangeLog]
public class Redirect : BaseEntity
{
    // --- Source ---

    [Required]
    public RedirectSourceType SourceType { get; set; }

    // Used when SourceType = InternalPath
    [Searchable]
    public string? FromPath { get; set; }

    // Used when SourceType = ContentSlug
    public string? FromLanguage { get; set; }

    public string? FromSlug { get; set; }

    // Used when SourceType = ContentId
    public int? FromContentId { get; set; }

    // --- Redirect behaviour ---

    [Required]
    public RedirectKind Kind { get; set; } = RedirectKind.Permanent;

    [Required]
    public RedirectTargetType TargetType { get; set; }

    // --- Target (populate the field matching TargetType) ---

    public string? ToUrl { get; set; }

    public string? ToPath { get; set; }

    public string? ToLanguage { get; set; }

    public string? ToSlug { get; set; }

    public int? ToContentId { get; set; }

    public bool IsAutoDiscovered { get; set; }

    public bool IsAutoDiscoverySuppressed { get; set; }
}


