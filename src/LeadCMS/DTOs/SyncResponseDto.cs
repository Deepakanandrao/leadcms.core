// <copyright file="SyncResponseDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json.Serialization;

namespace LeadCMS.DTOs;

/// <summary>
/// Typed response DTO for sync API endpoints.
/// Contains the changed items since the last sync and identifiers for deleted/removed entries.
/// </summary>
/// <typeparam name="TItem">The type of changed item DTOs (e.g. ContentDetailsDto, MediaDetailsDto).</typeparam>
/// <typeparam name="TDeleted">The type of deleted entry identifiers (e.g. int for entity IDs, MediaDeletedDto for file paths).</typeparam>
public class SyncResponseDto<TItem, TDeleted>
{
    /// <summary>
    /// Gets or sets the list of items that were created or modified since the last sync.
    /// </summary>
    public List<TItem> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of deleted entry identifiers since the last sync.
    /// For standard entities this is a list of integer IDs.
    /// For media this is a list of <see cref="MediaDeletedDto"/> containing the file paths.
    /// </summary>
    public List<TDeleted> Deleted { get; set; } = new();

    /// <summary>
    /// Gets or sets a dictionary mapping entity IDs to the base version of the item
    /// (the version that was current at the time of the client's sync token).
    /// Only populated when explicitly requested via the <c>includeBase</c> query parameter
    /// and a sync token is provided. Contains the entity state at the time of the sync token,
    /// i.e. the version the client last received. Clients can use this together with the
    /// current version in <see cref="Items"/> to perform a three-way merge of local changes
    /// with remote changes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, TItem>? BaseItems { get; set; }
}
