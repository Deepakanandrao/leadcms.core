// <copyright file="ContentTypeDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;
using LeadCMS.Entities;

namespace LeadCMS.DTOs;

public class ContentTypeCreateDto
{
    [Required]
    public string Uid { get; set; } = string.Empty;

    [Required]
    public ContentFormat Format { get; set; }

    public bool SupportsComments { get; set; } = false;

    public bool SupportsCoverImage { get; set; } = false;
}

public class ContentTypeUpdateDto
{
    public string? Uid { get; set; }

    public ContentFormat? Format { get; set; }

    public bool? SupportsComments { get; set; }

    public bool? SupportsCoverImage { get; set; }
}

public class ContentTypeDetailsDto : ContentTypeCreateDto
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int ContentCount { get; set; }
}

public class ContentTypeImportDto : BaseImportDto
{
    [Optional]
    public string? Uid { get; set; }

    [Optional]
    public ContentFormat? Format { get; set; }

    [Optional]
    public bool? SupportsComments { get; set; }

    [Optional]
    public bool? SupportsCoverImage { get; set; }
}
