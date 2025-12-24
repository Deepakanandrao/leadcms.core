// <copyright file="Segment.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LeadCMS.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

[Table("segment")]
[SupportsChangeLog]
[Index(nameof(Name), IsUnique = true)]
public class Segment : BaseEntity
{
    [Required]
    [Searchable]
    public string Name { get; set; } = string.Empty;

    [Searchable]
    public string? Description { get; set; }

    [Required]
    [Searchable]
    public string Type { get; set; } = string.Empty; // "dynamic" or "static"

    public int ContactCount { get; set; }

    [Column(TypeName = "jsonb")]
    public string? Definition { get; set; } // JSON string for SegmentDefinition

    [Column(TypeName = "integer[]")]
    public int[]? ContactIds { get; set; }

    [Searchable]
    [Column(TypeName = "text[]")]
    public string[]? Tags { get; set; }
}
