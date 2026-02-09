// <copyright file="BulkDeleteHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Helpers;

public static class BulkDeleteHelper
{
    /// <summary>
    /// Full bulk-delete flow for entities inheriting from <see cref="BaseEntityWithId"/>.
    /// Validates IDs, queries the given <paramref name="queryable"/>, checks for missing
    /// entities, removes them, saves, and optionally runs post-delete logic.
    /// </summary>
    /// <param name="dbContext">The EF Core context used to save changes.</param>
    /// <param name="queryable">
    /// The base queryable to fetch entities from.  May already include filters
    /// (e.g. <c>dbContext.ImapAccounts.Where(a => a.UserId == userId)</c>) or
    /// includes (e.g. <c>.Include(oi => oi.Order)</c>).
    /// </param>
    /// <param name="ids">The list of IDs sent in the request body.</param>
    /// <param name="entityName">
    /// Optional entity name for error messages.  Defaults to <c>typeof(TEntity).Name</c>.
    /// </param>
    /// <param name="customDelete">
    /// When provided, called instead of <c>dbContext.RemoveRange</c>.
    /// Use this for entities that need service-level delete logic.
    /// </param>
    /// <param name="onAfterDelete">
    /// Optional async callback invoked after <c>SaveChangesAsync</c>.
    /// </param>
    public static async Task<ActionResult> ExecuteAsync<TEntity>(
        DbContext dbContext,
        IQueryable<TEntity> queryable,
        List<int> ids,
        string? entityName = null,
        Action<List<TEntity>>? customDelete = null,
        Func<List<TEntity>, Task>? onAfterDelete = null)
        where TEntity : BaseEntityWithId
    {
        var invalidResult = ValidateIds(ids);
        if (invalidResult != null)
        {
            return invalidResult;
        }

        var distinctIds = ids.Distinct().ToList();

        var entitiesToDelete = await queryable
            .Where(entity => distinctIds.Contains(entity.Id))
            .ToListAsync();

        ThrowIfMissingIds(entityName ?? typeof(TEntity).Name, distinctIds, entitiesToDelete.Select(e => e.Id));

        if (customDelete != null)
        {
            customDelete(entitiesToDelete);
        }
        else
        {
            dbContext.RemoveRange(entitiesToDelete);
        }

        await dbContext.SaveChangesAsync();

        if (onAfterDelete != null)
        {
            await onAfterDelete(entitiesToDelete);
        }

        return new NoContentResult();
    }

    /// <summary>
    /// Returns a 422 result when the id list is null or empty, otherwise <c>null</c>.
    /// Useful for controllers that cannot use <see cref="ExecuteAsync{TEntity}"/>
    /// (e.g. when entity IDs are strings or deletion goes through a non-EF service).
    /// </summary>
    public static ActionResult? ValidateIds<TId>(IEnumerable<TId>? ids, string? detail = null)
    {
        if (ids == null || !ids.Any())
        {
            return new UnprocessableEntityObjectResult(new ProblemDetails
            {
                Title = "No IDs provided.",
                Detail = detail ?? "Provide at least one id in the request body.",
                Status = StatusCodes.Status422UnprocessableEntity,
            });
        }

        return null;
    }

    /// <summary>
    /// Throws <see cref="EntityNotFoundException"/> when <paramref name="foundIds"/>
    /// does not contain every element in <paramref name="requestedIds"/>.
    /// </summary>
    public static void ThrowIfMissingIds<TId>(string entityName, IEnumerable<TId> requestedIds, IEnumerable<TId> foundIds)
    {
        var found = new HashSet<TId>(foundIds);
        var missingIds = requestedIds.Where(id => !found.Contains(id)).ToList();
        if (missingIds.Count > 0)
        {
            throw new EntityNotFoundException(entityName, string.Join(",", missingIds));
        }
    }
}
