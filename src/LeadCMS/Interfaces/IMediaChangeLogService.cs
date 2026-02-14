// <copyright file="IMediaChangeLogService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for tracking media file changes (deletions and renames) in the ChangeLog
/// without persisting binary data. This enables the sync API to detect deleted
/// and renamed media files.
/// </summary>
public interface IMediaChangeLogService
{
    /// <summary>
    /// Logs a deletion entry for a media file. Creates a lightweight ChangeLog record
    /// containing only metadata (no binary data).
    /// </summary>
    /// <param name="media">The media entity being deleted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogMediaDeletedAsync(Media media);

    /// <summary>
    /// Logs deletion entries for multiple media files in a single batch.
    /// </summary>
    /// <param name="mediaList">The media entities being deleted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogMediaDeletedBatchAsync(IEnumerable<Media> mediaList);

    /// <summary>
    /// Logs a rename event by creating a Modified ChangeLog entry that preserves the old
    /// file path in its Data property. The sync service extracts the old path from Modified
    /// entries and returns it as a deleted path, so clients can remove the old local file.
    /// The renamed file itself will appear in the sync items with its new path.
    /// </summary>
    /// <param name="mediaId">The ID of the media entity being renamed.</param>
    /// <param name="oldScopeUid">The old scope/folder path before rename.</param>
    /// <param name="oldName">The old file name before rename.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogMediaRenamedAsync(int mediaId, string oldScopeUid, string oldName);
}
