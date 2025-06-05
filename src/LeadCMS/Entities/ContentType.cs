// <copyright file="ContentType.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LeadCMS.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

public enum ContentFormat
{
    MD = 0,
    MDX = 1,
    HTML = 2,
    JSON = 3,
    YAML = 4,
    PlainText = 5,
}

[Table("content_type")]
[Index(nameof(Uid), IsUnique = true)]
[SupportsChangeLog]
public class ContentType : BaseEntity
{
    [Required]
    [Searchable]
    public string Uid { get; set; } = string.Empty;

    [Required]
    public ContentFormat Format { get; set; }

    public bool SupportsComments { get; set; } = false;

    public bool SupportsCoverImage { get; set; } = false;
}
