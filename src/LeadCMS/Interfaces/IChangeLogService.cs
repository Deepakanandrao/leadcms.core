// <copyright file="IChangeLogService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for managing ChangeLog records.
/// </summary>
public interface IChangeLogService
{
    /// <summary>
    /// Safely parses JSON data into the specified UpdateDto type.
    /// Returns null if parsing fails rather than throwing an exception.
    /// </summary>
    /// <typeparam name="TUpdateDto">The type to parse the JSON into.</typeparam>
    /// <param name="jsonData">The JSON data to parse.</param>
    /// <returns>The parsed object or null if parsing failed.</returns>
    TUpdateDto? SafeParseData<TUpdateDto>(string jsonData)
        where TUpdateDto : class;

    /// <summary>
    /// Extracts the CreatedById field from the ChangeLog JSON data.
    /// </summary>
    /// <param name="jsonData">The JSON data to extract from.</param>
    /// <returns>The CreatedById value or null if not found.</returns>
    string? ExtractCreatedById(string jsonData);

    /// <summary>
    /// Extracts the UpdatedById field from the ChangeLog JSON data.
    /// </summary>
    /// <param name="jsonData">The JSON data to extract from.</param>
    /// <returns>The UpdatedById value or null if not found.</returns>
    string? ExtractUpdatedById(string jsonData);

    /// <summary>
    /// Resolves multiple user IDs to their display names in a single batch operation.
    /// </summary>
    /// <param name="userIds">The collection of user IDs to resolve.</param>
    /// <returns>A dictionary mapping user IDs to their display names.</returns>
    Task<Dictionary<string, string>> BatchResolveUserDisplayNamesAsync(IEnumerable<string?> userIds);
}