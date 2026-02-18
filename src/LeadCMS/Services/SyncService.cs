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
        string? query = null,
        bool includeBase = false)
        where TEntity : BaseEntityWithId, new()
        where TDto : class
    {
        return await SyncCoreAsync<TEntity, TDto, int>(
            queryProviderFactory,
            mapper,
            syncToken,
            includeBase,
            async (lastSyncTime) =>
            {
                var objectType = typeof(TEntity).Name;
                var deletedQuery = dbContext.ChangeLogs!.AsNoTracking()
                    .Where(cl => cl.ObjectType == objectType && cl.EntityState == EntityState.Deleted && cl.CreatedAt > lastSyncTime);

                var deletedIds = await deletedQuery.Select(cl => cl.ObjectId).Distinct().ToListAsync();

                DateTime? maxDeleted = deletedIds.Any()
                    ? await deletedQuery.MaxAsync(cl => (DateTime?)cl.CreatedAt)
                    : null;

                return new DeletedInfo<int>(deletedIds, maxDeleted);
            });
    }

    /// <inheritdoc/>
    public async Task<IActionResult> SyncMediaAsync(
        QueryProviderFactory<Media> queryProviderFactory,
        IMapper mapper,
        string? syncToken = null,
        string? query = null)
    {
        return await SyncCoreAsync<Media, MediaDetailsDto, MediaDeletedDto>(
            queryProviderFactory,
            mapper,
            syncToken,
            includeBase: false,
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

                return new DeletedInfo<MediaDeletedDto>(deletedPaths, changeLogMaxTime);
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
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TDto">The DTO type for changed items.</typeparam>
    /// <typeparam name="TDeleted">The type of deleted entry identifiers.</typeparam>
    private async Task<IActionResult> SyncCoreAsync<TEntity, TDto, TDeleted>(
        QueryProviderFactory<TEntity> queryProviderFactory,
        IMapper mapper,
        string? syncToken,
        bool includeBase,
        Func<DateTime, Task<DeletedInfo<TDeleted>>> resolveDeleted)
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

        // Resolve base versions from ChangeLog for three-way merge support.
        // Base versions are only relevant when explicitly requested via includeBase,
        // a syncToken is provided (incremental sync), and there are changed entities.
        Dictionary<int, TDto>? baseItems = null;
        if (includeBase && !string.IsNullOrEmpty(syncToken) && changedEntities.Any())
        {
            baseItems = new Dictionary<int, TDto>();
            // Identify entities that existed before lastSyncTime (modified, not newly created)
            var modifiedEntityIds = changedEntities
                .Where(e => e is IHasCreatedAt created && created.CreatedAt <= lastSyncTime)
                .Select(e => e.Id)
                .ToList();

            if (modifiedEntityIds.Any())
            {
                var objectType = typeof(TEntity).Name;

                // Fetch all non-deleted ChangeLog entries for the modified entities
                // and determine the base version using ordering rather than strict
                // timestamp comparison. This avoids precision issues where
                // ChangeLog.CreatedAt can differ slightly from entity timestamps
                // (they are set with separate DateTime.UtcNow calls in PgDbContext).
                var allChangeLogEntries = await dbContext.ChangeLogs!.AsNoTracking()
                    .Where(cl => cl.ObjectType == objectType
                        && modifiedEntityIds.Contains(cl.ObjectId)
                        && cl.EntityState != EntityState.Deleted)
                    .OrderBy(cl => cl.CreatedAt)
                    .ToListAsync();

                foreach (var group in allChangeLogEntries.GroupBy(e => e.ObjectId))
                {
                    var entries = group.OrderBy(e => e.CreatedAt).ToList();

                    // Find the first ChangeLog entry after lastSyncTime — this is
                    // the first change the client hasn't seen yet.
                    var firstPostSyncIndex = entries.FindIndex(e => e.CreatedAt > lastSyncTime);

                    ChangeLog? baseEntry = null;
                    if (firstPostSyncIndex > 0)
                    {
                        // Normal case: take the entry just before the first unseen change.
                        baseEntry = entries[firstPostSyncIndex - 1];
                    }
                    else if (firstPostSyncIndex == 0)
                    {
                        // All ChangeLog entries are after lastSyncTime due to timestamp
                        // drift. The entity existed before lastSyncTime (CreatedAt <= lastSyncTime),
                        // so the earliest entry is its creation state — use it as the base.
                        baseEntry = entries[0];
                    }
                    else
                    {
                        // No entries after lastSyncTime found (unlikely edge case).
                        // Use the most recent entry as the base.
                        baseEntry = entries.Last();
                    }

                    if (baseEntry != null)
                    {
                        try
                        {
                            var baseEntity = JsonHelper.Deserialize<TEntity>(baseEntry.Data);
                            if (baseEntity != null)
                            {
                                var baseDto = mapper.Map<TDto>(baseEntity);
                                baseItems[group.Key] = baseDto;
                            }
                        }
                        catch
                        {
                            // Skip entries that fail to deserialize — the base version
                            // won't be available for this entity but sync continues.
                        }
                    }
                }

                if (baseItems.Any())
                {
                    DtoCleanupHelper.RemoveSecondLevelObjects(baseItems.Values.ToList());
                }
            }
        }

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

        if (deletedInfo.MaxTime != null && (maxTime == null || deletedInfo.MaxTime > maxTime))
        {
            maxTime = deletedInfo.MaxTime;
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

        if (items.Count == 0 && deletedInfo.Payload.Count == 0)
        {
            return new NoContentResult();
        }

        var syncResponse = new SyncResponseDto<TDto, TDeleted>
        {
            Items = items,
            Deleted = deletedInfo.Payload,
            BaseItems = baseItems,
        };

        return new OkObjectResult(syncResponse);
    }

    /// <summary>
    /// Holds the result of the deleted-data resolution strategy, carrying the typed
    /// payload and the maximum ChangeLog timestamp for sync-token calculation.
    /// </summary>
    /// <typeparam name="T">The type of deleted entry identifiers.</typeparam>
    private sealed class DeletedInfo<T>
    {
        public DeletedInfo(List<T> payload, DateTime? maxTime)
        {
            Payload = payload;
            MaxTime = maxTime;
        }

        /// <summary>
        /// Gets the typed list of deleted entry identifiers.
        /// </summary>
        public List<T> Payload { get; }

        /// <summary>
        /// Gets the maximum ChangeLog CreatedAt among the resolved entries, used for sync token calculation.
        /// </summary>
        public DateTime? MaxTime { get; }
    }
}