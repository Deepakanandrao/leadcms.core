// <copyright file="SegmentDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LeadCMS.DTOs;

public class SegmentDetailsDto
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public string Type { get; set; } = string.Empty; // "dynamic" or "static"

    public int ContactCount { get; set; }

    public SegmentDefinition? Definition { get; set; }

    public int[]? ContactIds { get; set; }

    public string[]? Tags { get; set; }

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
    public string Type { get; set; } = string.Empty; // "dynamic" or "static"

    public SegmentDefinition? Definition { get; set; }

    public int[]? ContactIds { get; set; }

    public string[]? Tags { get; set; }
}

public class SegmentUpdateDto
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public SegmentDefinition? Definition { get; set; }

    public int[]? ContactIds { get; set; }

    public string[]? Tags { get; set; }
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
    [JsonConverter(typeof(JsonStringEnumConverter))]
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
    [JsonConverter(typeof(JsonStringEnumConverter))]
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

public class ContactSummaryDto
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string AvatarUrl { get; set; } = string.Empty;
}
