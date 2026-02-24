// <copyright file="EmailTemplateDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.Enums;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class EmailTemplateCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string BodyTemplate { get; set; } = string.Empty;

    public EmailTemplateCategory Category { get; set; } = EmailTemplateCategory.General;

    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = string.Empty;

    [Required]
    public string Language { get; set; } = string.Empty;

    public string? TranslationKey { get; set; }

    [Required]
    public int EmailGroupId { get; set; }
}

public class EmailTemplateUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [MinLength(1)]
    public string? Name { get; set; }

    [MinLength(1)]
    public string? Subject { get; set; }

    [MinLength(1)]
    public string? BodyTemplate { get; set; }

    public EmailTemplateCategory? Category { get; set; }

    [EmailAddress]
    public string? FromEmail { get; set; }

    [MinLength(1)]
    public string? FromName { get; set; }

    public string? Language { get; set; }

    public string? TranslationKey { get; set; }

    public int? EmailGroupId { get; set; }
}

public class EmailTemplateDetailsDto : EmailTemplateCreateDto
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Ignore]
    public EmailGroupDetailsDto? EmailGroup { get; set; }
}