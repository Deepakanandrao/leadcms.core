// <copyright file="SyncService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using System.Text.Json;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Exceptions;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

/// <summary>
/// Service for handling synchronization operations across different entity types.
/// Extracts the sync logic from BaseController to make it reusable by any controller.
/// </summary>
public class SyncService : ISyncService
{
    private readonly PgDbContext dbContext;
    private readonly IHttpContextAccessor httpContextAccessor;

    public SyncService(PgDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        this.dbContext = dbContext;
        this.httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<IActionResult> SyncAsync<TEntity, TDto>(
        QueryProviderFactory<TEntity> queryProviderFactory,
        IMapper mapper,
        string? syncToken = null,
        string? query = null)
        where TEntity : BaseEntityWithId, new()
        where TDto : class
    {
        return await SyncCoreAsync<TEntity, TDto>(
            queryProviderFactory,
            mapper,
            syncToken,
            async (lastSyncTime) =>
            {
                var objectType = typeof(TEntity).Name;
                var deletedQuery = dbContext.ChangeLogs!.AsNoTracking()
                    .Where(cl => cl.ObjectType == objectType && cl.EntityState == EntityState.Deleted && cl.CreatedAt > lastSyncTime);

                var deletedIds = await deletedQuery.Select(cl => cl.ObjectId).Distinct().ToListAsync();

                DateTime? maxDeleted = deletedIds.Any()
                    ? await deletedQuery.MaxAsync(cl => (DateTime?)cl.CreatedAt)
                    : null;

                return new DeletedInfo(deletedIds, deletedIds.Count, maxDeleted);
            });
    }

    /// <inheritdoc/>
    public async Task<IActionResult> SyncMediaAsync(
        QueryProviderFactory<Media> queryProviderFactory,
        IMapper mapper,
        string? syncToken = null,
        string? query = null)
    {
        return await SyncCoreAsync<Media, MediaDetailsDto>(
            queryProviderFactory,
            mapper,
            syncToken,
            async (lastSyncTime) =>
            {
                // Get deleted file paths from ChangeLog (Deleted entries)
                var deletedChangeLogs = await dbContext.ChangeLogs!.AsNoTracking()
                    .Where(cl => cl.ObjectType == nameof(Media) && cl.EntityState == EntityState.Deleted && cl.CreatedAt > lastSyncTime)
                    .Select(cl => cl.Data)
                    .ToListAsync();

                var deletedPaths = new List<MediaDeletedDto>();
                foreach (var data in deletedChangeLogs)
                {
                    var parsed = ParseDeletedMediaPath(data);
                    if (parsed != null)
                    {
                        deletedPaths.Add(parsed);
                    }
                }

                // Get old paths from renamed files (Modified entries) — these represent paths that no longer exist
                var renamedChangeLogs = await dbContext.ChangeLogs!.AsNoTracking()
                    .Where(cl => cl.ObjectType == nameof(Media) && cl.EntityState == EntityState.Modified && cl.CreatedAt > lastSyncTime)
                    .Select(cl => cl.Data)
                    .ToListAsync();

                foreach (var data in renamedChangeLogs)
                {
                    var parsed = ParseRenamedMediaOldPath(data);
                    if (parsed != null)
                    {
                        deletedPaths.Add(parsed);
                    }
                }

                // Max changelog time across both Deleted and Modified
                var changeLogMaxTime = await dbContext.ChangeLogs!.AsNoTracking()
                    .Where(cl => cl.ObjectType == nameof(Media) && cl.CreatedAt > lastSyncTime &&
                        (cl.EntityState == EntityState.Deleted || cl.EntityState == EntityState.Modified))
                    .Select(cl => (DateTime?)cl.CreatedAt)
                    .MaxAsync() ?? null;

                return new DeletedInfo(deletedPaths, deletedPaths.Count, changeLogMaxTime);
            });
    }

    /// <summary>
    /// Parses the Data JSON of a Deleted ChangeLog entry to extract the scopeUid and name.
    /// </summary>
    private static MediaDeletedDto? ParseDeletedMediaPath(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            var scopeUid = root.TryGetProperty("scopeUid", out var s) ? s.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;

            if (!string.IsNullOrEmpty(scopeUid) && !string.IsNullOrEmpty(name))
            {
                return new MediaDeletedDto { ScopeUid = scopeUid, Name = name };
            }
        }
        catch
        {
            // Ignore malformed data
        }

        return null;
    }

    /// <summary>
    /// Parses the Data JSON of a Modified (renamed) ChangeLog entry to extract the old path.
    /// </summary>
    private static MediaDeletedDto? ParseRenamedMediaOldPath(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            var oldScopeUid = root.TryGetProperty("oldScopeUid", out var s) ? s.GetString() : null;
            var oldName = root.TryGetProperty("oldName", out var n) ? n.GetString() : null;

            if (!string.IsNullOrEmpty(oldScopeUid) && !string.IsNullOrEmpty(oldName))
            {
                return new MediaDeletedDto { ScopeUid = oldScopeUid, Name = oldName };
            }
        }
        catch
        {
            // Ignore malformed data
        }

        return null;
    }

    /// <summary>
    /// Core sync logic shared by all entity types. The <paramref name="resolveDeleted"/> delegate
    /// is responsible for querying the ChangeLog and returning the deleted payload (which can be
    /// a list of IDs for standard entities or a list of path DTOs for media).
    /// </summary>
    private async Task<IActionResult> SyncCoreAsync<TEntity, TDto>(
        QueryProviderFactory<TEntity> queryProviderFactory,
        IMapper mapper,
        string? syncToken,
        Func<DateTime, Task<DeletedInfo>> resolveDeleted)
        where TEntity : BaseEntityWithId, new()
        where TDto : class
    {
        var now = DateTime.UtcNow;
        DateTime lastSyncTime = DateTime.MinValue;

        if (!string.IsNullOrEmpty(syncToken))
        {
            if (!SyncTokenHelper.TryDecodeSyncToken(syncToken, out lastSyncTime))
            {
                throw new QueryException("syncToken", "Malformed sync token.");
            }

            if (lastSyncTime > now)
            {
                throw new QueryException("syncToken", "Sync token is from the future.");
            }
        }

        var qp = queryProviderFactory.BuildQueryProvider();
        var dbQueryProvider = qp as DBQueryProvider<TEntity>;
        var dbSet = dbContext.Set<TEntity>();
        IQueryable<TEntity> queryable = dbQueryProvider != null ? dbQueryProvider.BuiltQuery : dbSet.AsNoTracking();

        // EF Core cannot translate interface casts, so fetch and filter in memory
        // TODO: Optimize this if possible
        var allEntities = await queryable.ToListAsync();
        var changedEntities = allEntities.Where(e =>
            (e is IHasUpdatedAt updated && updated.UpdatedAt != null && updated.UpdatedAt > lastSyncTime) ||
            (e is IHasCreatedAt created && created.CreatedAt > lastSyncTime))
            .ToList();

        var items = mapper.Map<List<TDto>>(changedEntities);
        DtoCleanupHelper.RemoveSecondLevelObjects(items);

        // Resolve deleted data via the strategy delegate
        var deletedInfo = await resolveDeleted(lastSyncTime);

        // Determine nextSyncToken (max updated_at/created_at/deleted)
        DateTime? maxTime = null;
        if (changedEntities.Any())
        {
            List<DateTime?> allTimes = new List<DateTime?>();
            foreach (var e in changedEntities)
            {
                DateTime? t = null;
                if (e is IHasUpdatedAt updated && updated.UpdatedAt != null)
                {
                    t = updated.UpdatedAt;
                }
                else if (e is IHasCreatedAt created)
                {
                    t = created.CreatedAt;
                }

                allTimes.Add(t);
            }

            var maxUpdated = allTimes.Where(dt => dt != null).Max();
            if (maxUpdated != null)
            {
                maxTime = maxUpdated;
            }
        }

        if (deletedInfo.MaxChangeLogTime != null && (maxTime == null || deletedInfo.MaxChangeLogTime > maxTime))
        {
            maxTime = deletedInfo.MaxChangeLogTime;
        }

        // Use lastSyncTime as nextSyncTime if no new maxTime is found
        var nextSyncTime = maxTime ?? lastSyncTime;
        var token = SyncTokenHelper.EncodeSyncToken(nextSyncTime);

        var response = httpContextAccessor.HttpContext?.Response;
        if (response != null)
        {
            response.Headers.Append(ResponseHeaderNames.NextSyncToken, token);
            response.Headers.Append(ResponseHeaderNames.TotalCount, (dbQueryProvider?.BuiltQuery.Count() ?? items.Count).ToString());
            response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);
        }

        if (items.Count == 0 && deletedInfo.Count == 0)
        {
            return new NoContentResult();
        }

        return new OkObjectResult(new { items, deleted = deletedInfo.Payload });
    }

    /// <summary>
    /// Holds the result of the deleted-data resolution strategy, carrying the serialisable
    /// payload, its count, and the maximum ChangeLog timestamp for sync-token calculation.
    /// </summary>
    private sealed class DeletedInfo
    {
        public DeletedInfo(object payload, int count, DateTime? maxChangeLogTime)
        {
            Payload = payload;
            Count = count;
            MaxChangeLogTime = maxChangeLogTime;
        }

        /// <summary>
        /// Gets the serialisable deleted data (e.g. <c>List&lt;int&gt;</c> or <c>List&lt;MediaDeletedDto&gt;</c>).
        /// </summary>
        public object Payload { get; }

        /// <summary>
        /// Gets the number of deleted entries.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the maximum ChangeLog CreatedAt among the resolved entries, used for sync token calculation.
        /// </summary>
        public DateTime? MaxChangeLogTime { get; }
    }
}