// <copyright file="CommentDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class CommentCreateBaseDto
{
    private string authorEmail = string.Empty;

    [Required]
    [EmailAddress]
    public string AuthorEmail
    {
        get
        {
            return authorEmail;
        }

        set
        {
            authorEmail = value.ToLower();
        }
    }

    public string AuthorName { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public int? ContactId { get; set; }

    public int? ParentId { get; set; }

    [Optional]
    public string? Source { get; set; }

    public string Language { get; set; } = string.Empty;

    public string? TranslationKey { get; set; }

    public string[]? Tags { get; set; }
}

public class CommentCreateDto : CommentCreateBaseDto
{
    public int? CommentableId { get; set; }

    public string? CommentableUid { get; set; }

    [Required]
    public string CommentableType { get; set; } = string.Empty;
}

public class CommentUpdateDto : IPatchDto
{
    private string? authorEmail;

    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string? Body { get; set; }

    public string? AuthorName { get; set; }

    [EmailAddress]
    public string? AuthorEmail
    {
        get
        {
            return authorEmail;
        }

        set
        {
            authorEmail = string.IsNullOrEmpty(value) ? null : value.ToLower();
        }
    }

    public string? Language { get; set; }

    public CommentStatus? Status { get; set; }

    public AnswerStatus? AnswerStatus { get; set; }

    public string? TranslationKey { get; set; }

    public string[]? Tags { get; set; }
}

public class AnonymousCommentDetailsDto
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int CommentableId { get; set; }

    public string CommentableType { get; set; } = string.Empty;

    public string AvatarUrl { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public string? TranslationKey { get; set; }

    [Ignore]
    public ContentDetailsDto? Content { get; set; }

    [Ignore]
    public CommentDetailsDto? Parent { get; set; }
}

public class CommentDetailsDto : AnonymousCommentDetailsDto
{
    public string AuthorEmail { get; set; } = string.Empty;

    public int? ContactId { get; set; }

    public string? Source { get; set; }

    [Ignore]

    public ContactDetailsDto? Contact { get; set; }

    public string[]? Tags { get; set; }

    public CommentStatus Status { get; set; } = CommentStatus.NotApproved;

    public AnswerStatus AnswerStatus { get; set; } = AnswerStatus.Unanswered;
}

public class CommentImportDto : BaseImportDto
{
    private string? authorEmail;

    [Optional]
    public int? ContactId { get; set; }

    [Optional]
    public string? AuthorName { get; set; }

    [Optional]
    [EmailAddress]
    [SurrogateForeignKey(typeof(Contact), "Email", "ContactId")]
    public string? AuthorEmail
    {
        get
        {
            return authorEmail;
        }

        set
        {
            authorEmail = string.IsNullOrEmpty(value) ? null : value.ToLower();
        }
    }

    [Optional]
    public string? Body { get; set; }

    [Optional]
    public CommentStatus? Status { get; set; }

    [Optional]
    public string? Language { get; set; }

    [Optional]
    public string? TranslationKey { get; set; }

    [Optional]
    public int? CommentableId { get; set; }

    [Optional]
    public string? CommentableType { get; set; }

    [Optional]
    public int? ParentId { get; set; }

    [Optional]
    public string? Key { get; set; }

    [Optional]
    [SurrogateForeignKey(typeof(Comment), "Key", "ParentId")]
    public string? ParentKey { get; set; }

    [Optional]
    public string[]? Tags { get; set; }

    [Optional]
    public AnswerStatus? AnswerStatus { get; set; }
}

public class CommentsWithStatisticsDto
{
    public List<CommentDetailsDto> Comments { get; set; } = new List<CommentDetailsDto>();

    public Dictionary<string, long> Statistics { get; set; } = new Dictionary<string, long>();
}

public class AnonymousCommentsWithStatisticsDto
{
    public List<AnonymousCommentDetailsDto> Comments { get; set; } = new List<AnonymousCommentDetailsDto>();

    public Dictionary<string, long> Statistics { get; set; } = new Dictionary<string, long>();
}