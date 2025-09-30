// <copyright file="SyncService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using AutoMapper;
using LeadCMS.Data;
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

        // Get deletions from ChangeLog
        var objectType = typeof(TEntity).Name;
        var deletedQuery = dbContext.ChangeLogs!.AsNoTracking()
            .Where(cl => cl.ObjectType == objectType && cl.EntityState == EntityState.Deleted && cl.CreatedAt > lastSyncTime);
        var deletedIds = await deletedQuery.Select(cl => cl.ObjectId).Distinct().ToListAsync();

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

        if (deletedIds.Any())
        {
            var maxDeleted = await deletedQuery.MaxAsync(cl => (DateTime?)cl.CreatedAt);
            if (maxDeleted != null && (maxTime == null || maxDeleted > maxTime))
            {
                maxTime = maxDeleted;
            }
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

        if (items.Count == 0 && deletedIds.Count == 0)
        {
            return new NoContentResult();
        }

        return new OkObjectResult(new { items, deleted = deletedIds });
    }
}