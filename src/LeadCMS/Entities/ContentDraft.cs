// <copyright file="ContentDraft.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

[Table("content_draft")]
[Index(nameof(ObjectId), nameof(ObjectType), nameof(CreatedById), IsUnique = true)]
public class ContentDraft : BaseEntity
{
    [Required]
    public string ObjectType { get; set; } = string.Empty;

    [Required]
    public int ObjectId { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = string.Empty;
}
