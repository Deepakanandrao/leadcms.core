// <copyright file="Setting.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LeadCMS.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

[Table("setting")]
[SupportsChangeLog]
[Index(nameof(Key), nameof(UserId), IsUnique = true)]
public class Setting : BaseEntity
{
    [Required]
    [MaxLength(255)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? UserId { get; set; }
}
