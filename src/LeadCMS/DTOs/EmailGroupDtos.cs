// <copyright file="EmailGroupDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;

namespace LeadCMS.DTOs;

public class EmailGroupCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Language { get; set; } = string.Empty;

    public string? TranslationKey { get; set; }
}

public class EmailGroupUpdateDto
{
    [MinLength(1)]
    public string? Name { get; set; }

    public string? Language { get; set; }

    public string? TranslationKey { get; set; }
}

public class EmailGroupDetailsDto : EmailGroupCreateDto
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Ignore]
    public List<EmailTemplateDetailsDto>? EmailTemplates { get; set; }
}