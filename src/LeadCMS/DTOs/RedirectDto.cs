// <copyright file="RedirectDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using LeadCMS.Enums;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class RedirectCreateDto
{
    private string? fromPath;
    private string? fromSlug;
    private string? toPath;
    private string? toSlug;

    // --- Source ---

    [Required]
    public RedirectSourceType SourceType { get; set; }

    // Used when SourceType = InternalPath
    public string? FromPath { get => fromPath; set => fromPath = value?.Trim().Trim('/'); }

    // Used when SourceType = ContentSlug
    public string? FromLanguage { get; set; }

    public string? FromSlug { get => fromSlug; set => fromSlug = value?.Trim().Trim('/'); }

    // Used when SourceType = ContentId
    public int? FromContentId { get; set; }

    // --- Redirect behaviour ---

    [Required]
    public RedirectKind Kind { get; set; } = RedirectKind.Permanent;

    [Required]
    public RedirectTargetType TargetType { get; set; }

    // --- Target (populate the field matching TargetType) ---

    public string? ToUrl { get; set; }

    public string? ToPath { get => toPath; set => toPath = value?.Trim().Trim('/'); }

    public string? ToLanguage { get; set; }

    public string? ToSlug { get => toSlug; set => toSlug = value?.Trim().Trim('/'); }

    public int? ToContentId { get; set; }
}

public class RedirectUpdateDto : IPatchDto
{
    private string? fromPath;
    private string? fromSlug;
    private string? toPath;
    private string? toSlug;

    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public RedirectSourceType? SourceType { get; set; }

    public string? FromPath { get => fromPath; set => fromPath = value?.Trim().Trim('/'); }

    public string? FromLanguage { get; set; }

    public string? FromSlug { get => fromSlug; set => fromSlug = value?.Trim().Trim('/'); }

    public int? FromContentId { get; set; }

    public RedirectKind? Kind { get; set; }

    public RedirectTargetType? TargetType { get; set; }

    public string? ToUrl { get; set; }

    public string? ToPath { get => toPath; set => toPath = value?.Trim().Trim('/'); }

    public string? ToLanguage { get; set; }

    public string? ToSlug { get => toSlug; set => toSlug = value?.Trim().Trim('/'); }

    public int? ToContentId { get; set; }
}

public class RedirectDetailsDto : RedirectCreateDto
{
    public int Id { get; set; }

    public bool IsAutoDiscovered { get; set; }

    public bool IsAutoDiscoverySuppressed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
