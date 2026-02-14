// <copyright file="MediaChangeLogService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

/// <summary>
/// Creates lightweight ChangeLog entries for media file changes (deletions and renames)
/// without persisting binary data. This enables the sync API to detect
/// deleted/renamed media files while keeping the ChangeLog table small.
/// </summary>
public class MediaChangeLogService : IMediaChangeLogService
{
    private readonly PgDbContext dbContext;

    public MediaChangeLogService(PgDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task LogMediaDeletedAsync(Media media)
    {
        var changeLog = CreateMediaChangeLog(media, EntityState.Deleted);
        dbContext.ChangeLogs!.Add(changeLog);
        await dbContext.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task LogMediaDeletedBatchAsync(IEnumerable<Media> mediaList)
    {
        var changeLogs = mediaList.Select(m => CreateMediaChangeLog(m, EntityState.Deleted)).ToList();
        if (changeLogs.Count > 0)
        {
            dbContext.ChangeLogs!.AddRange(changeLogs);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task LogMediaRenamedAsync(int mediaId, string oldScopeUid, string oldName)
    {
        var changeLog = new ChangeLog
        {
            ObjectType = nameof(Media),
            ObjectId = mediaId,
            EntityState = EntityState.Modified,
            CreatedAt = DateTime.UtcNow,
            Data = JsonHelper.Serialize(new
            {
                Id = mediaId,
                OldScopeUid = oldScopeUid,
                OldName = oldName,
            }),
        };

        dbContext.ChangeLogs!.Add(changeLog);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a ChangeLog entry for a media entity with only metadata (no binary data).
    /// </summary>
    private static ChangeLog CreateMediaChangeLog(Media media, EntityState state)
    {
        return new ChangeLog
        {
            ObjectType = nameof(Media),
            ObjectId = media.Id,
            EntityState = state,
            CreatedAt = DateTime.UtcNow,
            Data = SerializeMediaMetadata(media),
        };
    }

    /// <summary>
    /// Serializes only the metadata properties of a media entity,
    /// explicitly excluding the binary Data and OriginalData fields.
    /// </summary>
    private static string SerializeMediaMetadata(Media media)
    {
        return JsonHelper.Serialize(new
        {
            media.Id,
            media.ScopeUid,
            media.Name,
            media.OriginalName,
            media.Description,
            media.Size,
            media.OriginalSize,
            media.Width,
            media.Height,
            media.OriginalWidth,
            media.OriginalHeight,
            media.Extension,
            media.OriginalExtension,
            media.MimeType,
            media.OriginalMimeType,
            media.Tags,
            media.UsageCount,
            media.CreatedAt,
            media.UpdatedAt,
        });
    }
}
