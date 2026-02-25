// <copyright file="Segment.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LeadCMS.DataAnnotations;
using LeadCMS.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

[Table("segment")]
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
    public SegmentType Type { get; set; } = SegmentType.Dynamic;

    public int ContactCount { get; set; }

    [Column(TypeName = "jsonb")]
    public SegmentDefinition? Definition { get; set; }

    [Column(TypeName = "integer[]")]
    public int[]? ContactIds { get; set; }
}

public enum SegmentType
{
    Dynamic,
    Static,
}
