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
[Index(nameof(Key), nameof(UserId), nameof(Language), IsUnique = true)]
public class Setting : BaseEntity
{
    [Required]
    [MaxLength(255)]
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the optional language code (e.g. "en", "de"). When null, the setting is language-neutral (general).
    /// Language-specific settings override general settings.
    /// </summary>
    [MaxLength(10)]
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this setting is required to be provided by the user.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the data type of the setting value (e.g. "string", "bool", "int", "json").
    /// Used by the client to render appropriate input controls.
    /// </summary>
    [MaxLength(50)]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of the setting for the admin UI.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
}
