// <copyright file="Content.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

[Table("content")]
[SupportsElastic]
[SupportsChangeLog]
[Index(nameof(Slug), nameof(Language), IsUnique = true)]
public class Content : BaseEntity, ICommentable, ITranslatable
{
    [Searchable]
    [Required]
    public string Title { get; set; } = string.Empty;

    [Searchable]
    [Required]
    public string Description { get; set; } = string.Empty;

    [Searchable]
    [Required]
    public string Body { get; set; } = string.Empty;

    public string? CoverImageUrl { get; set; }

    public string? CoverImageAlt { get; set; }

    [Searchable]
    [Required]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [ForeignKey(nameof(ContentType))]
    public string Type { get; set; } = string.Empty;

    [JsonIgnore]
    public virtual ContentType? ContentType { get; set; }

    [Searchable]
    [Required]
    public string Author { get; set; } = string.Empty;

    [Searchable]
    [Required]
    public string Language { get; set; } = string.Empty;

    public string? TranslationKey { get; set; }

    [Searchable]
    public string Category { get; set; } = string.Empty;

    [Searchable]
    public string[] Tags { get; set; } = Array.Empty<string>();

    public bool AllowComments { get; set; } = false;

    public DateTime? PublishedAt { get; set; } = DateTime.UtcNow;

    public static string GetCommentableType()
    {
        return "Content";
    }
}