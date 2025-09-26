// <copyright file="ChangeLogDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace LeadCMS.DTOs;

/// <summary>
/// DTO for ChangeLog records with strongly-typed parsed data.
/// </summary>
/// <typeparam name="T">The type of the UpdateDto to parse the Data field into.</typeparam>
public class ChangeLogDetailsDto<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the unique identifier for this change log record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the type name of the object that was changed.
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the object that was changed.
    /// </summary>
    public int ObjectId { get; set; }

    /// <summary>
    /// Gets or sets the entity state representing the type of change (Added, Modified, Deleted).
    /// </summary>
    public EntityState EntityState { get; set; }

    /// <summary>
    /// Gets or sets the parsed data as a strongly-typed UpdateDto instance.
    /// This represents the state of the object after the change was made.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this change was recorded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the source of the change (optional).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who created the entity (extracted from JSON Data).
    /// </summary>
    public string? CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who updated the entity (extracted from JSON Data).
    /// </summary>
    public string? UpdatedById { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user who created the entity.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user who updated the entity.
    /// </summary>
    public string? UpdatedBy { get; set; }
}