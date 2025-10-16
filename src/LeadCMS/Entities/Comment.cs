// <copyright file="Comment.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using LeadCMS.Interfaces;

namespace LeadCMS.Entities;

public enum CommentStatus
{
    NotApproved = 0,
    Approved = 1,
    Spam = 2,
    Answer = 3,
}

public enum AnswerStatus
{
    Unanswered = 0,      // Default - needs response from internal team
    Answered = 1,        // Has been answered by internal team
    Closed = 2,          // Conversation closed - no response needed/allowed
}

[Table("comment")]
[SupportsElastic]
[SupportsChangeLog]
[SurrogateIdentity(nameof(Key))]
public class Comment : BaseEntity, ITranslatable
{
    private string authorEmail = string.Empty;

    [Required]
    [Searchable]
    public string AuthorName { get; set; } = string.Empty;

    [Required]
    [Searchable]
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

    /// <summary>
    /// Gets or sets reference to a contact table.
    /// </summary>
    [Required]
    public int ContactId { get; set; }

    [JsonIgnore]
    [ForeignKey("ContactId")]
    public virtual Contact? Contact { get; set; }

    [Searchable]
    [Required]
    public string Body { get; set; } = string.Empty;

    [Searchable]
    [Required]
    public string Language { get; set; } = string.Empty;

    [Searchable]
    public string? TranslationKey { get; set; }

    public CommentStatus Status { get; set; } = CommentStatus.NotApproved;

    [Required]
    public int CommentableId { get; set; }

    [Required]
    public string CommentableType { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    [JsonIgnore]
    [ForeignKey("ParentId")]
    public virtual Comment? Parent { get; set; }

    public string Key { get; set; } = string.Empty;

    [Searchable]
    [Column(TypeName = "jsonb")]
    public string[]? Tags { get; set; }

    /// <summary>
    /// Gets or sets the answer status of the comment.
    /// </summary>
    public AnswerStatus AnswerStatus { get; set; } = AnswerStatus.Unanswered;
}