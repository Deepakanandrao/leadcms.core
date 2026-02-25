// <copyright file="SegmentDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class SegmentDetailsDto
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public SegmentType Type { get; set; } = SegmentType.Dynamic;

    public int ContactCount { get; set; }

    public SegmentDefinition? Definition { get; set; }

    public int[]? ContactIds { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedById { get; set; }

    public string? UpdatedById { get; set; }

    public string? CreatedByIp { get; set; }

    public string? CreatedByUserAgent { get; set; }

    public string? UpdatedByIp { get; set; }

    public string? UpdatedByUserAgent { get; set; }
}

public class SegmentCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public SegmentType Type { get; set; } = SegmentType.Dynamic;

    public SegmentDefinition? Definition { get; set; }

    public int[]? ContactIds { get; set; }
}

public class SegmentUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string? Name { get; set; }

    public string? Description { get; set; }

    public SegmentDefinition? Definition { get; set; }

    public int[]? ContactIds { get; set; }
}

public class SegmentDefinition
{
    [Required]
    public RuleGroup IncludeRules { get; set; } = new RuleGroup();

    public RuleGroup? ExcludeRules { get; set; }
}

public class RuleGroup
{
    [Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public RuleConnector Connector { get; set; } = RuleConnector.And;

    public List<SegmentRule> Rules { get; set; } = new List<SegmentRule>();

    public List<RuleGroup> Groups { get; set; } = new List<RuleGroup>();
}

public class SegmentRule
{
    [Required]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string FieldId { get; set; } = string.Empty;

    [Required]

    public FieldOperator Operator { get; set; }

    public object? Value { get; set; }
}

public enum RuleConnector
{
    And,
    Or,
}

public enum FieldOperator
{
    Equals,
    NotEquals,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    IsEmpty,
    IsNotEmpty,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    IsTrue,
    IsFalse,
    In,
    NotIn,
}

public class SegmentPreviewResultDto
{
    public int ContactCount { get; set; }

    public List<ContactDetailsDto> Contacts { get; set; } = new List<ContactDetailsDto>();
}
