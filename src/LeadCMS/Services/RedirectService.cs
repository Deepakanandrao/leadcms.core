// <copyright file="RedirectService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Exceptions;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

public class RedirectService : IRedirectService
{
    private readonly PgDbContext dbContext;

    public RedirectService(PgDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task DiscoverAsync()
    {
        var discovered = await RunDiscoverySqlAsync();

        foreach (var item in discovered)
        {
            // Skip any source that the user has explicitly suppressed.
            var suppressed = await dbContext.Set<Redirect>()
                .IgnoreQueryFilters()
                .AnyAsync(r =>
                    r.SourceType == RedirectSourceType.ContentSlug &&
                    r.FromLanguage == item.OldLanguage &&
                    r.FromSlug == item.OldSlug &&
                    r.IsAutoDiscoverySuppressed);

            if (suppressed)
            {
                continue;
            }

            var existing = await dbContext.Set<Redirect>()
                .FirstOrDefaultAsync(r =>
                    r.SourceType == RedirectSourceType.ContentSlug &&
                    r.FromLanguage == item.OldLanguage &&
                    r.FromSlug == item.OldSlug);

            if (existing == null)
            {
                dbContext.Set<Redirect>().Add(new Redirect
                {
                    SourceType = RedirectSourceType.ContentSlug,
                    FromLanguage = item.OldLanguage,
                    FromSlug = item.OldSlug,
                    Kind = RedirectKind.Temporary,
                    TargetType = RedirectTargetType.ContentSlug,
                    ToLanguage = item.NewLanguage,
                    ToSlug = item.NewSlug,
                    IsAutoDiscovered = true,
                });
            }
            else if (existing.IsAutoDiscovered)
            {
                // Only update auto-discovered entries; preserve manual edits.
                existing.ToLanguage = item.NewLanguage;
                existing.ToSlug = item.NewSlug;
                existing.TargetType = RedirectTargetType.ContentSlug;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task ValidateAsync(RedirectCreateDto dto, int? excludeId = null)
    {
        await CheckNoCycleAsync(dto, excludeId);
    }

    /// <summary>
    /// Returns true when the redirect <paramref name="r"/> points TO the same address as
    /// the new redirect's SOURCE — i.e. following the chain through <paramref name="r"/> would
    /// lead back to where the new redirect starts, forming a cycle.
    /// </summary>
    private static bool TargetMatchesDtoSource(Redirect r, RedirectCreateDto dto)
    {
        return dto.SourceType switch
        {
            RedirectSourceType.InternalPath =>
                r.TargetType == RedirectTargetType.InternalPath && r.ToPath == dto.FromPath,

            RedirectSourceType.ContentSlug =>
                r.TargetType == RedirectTargetType.ContentSlug &&
                r.ToLanguage == dto.FromLanguage &&
                r.ToSlug == dto.FromSlug,

            RedirectSourceType.ContentId =>
                r.TargetType == RedirectTargetType.ContentId && r.ToContentId == dto.FromContentId,

            _ => false,
        };
    }

    private async Task CheckNoCycleAsync(RedirectCreateDto dto, int? excludeId)
    {
        const int maxHops = 20;

        // Walk the chain starting from the new redirect's *target* and see whether
        // we ever reach back to the new redirect's *source*.
        var visitedIds = new HashSet<int>();
        var current = await FindRedirectByTargetAsync(dto);

        for (var hop = 0; hop < maxHops && current != null; hop++)
        {
            if (excludeId.HasValue && current.Id == excludeId.Value)
            {
                // This is the record being updated — don't count its old source as a cycle.
                break;
            }

            if (!visitedIds.Add(current.Id))
            {
                break; // Existing cycle unrelated to the new entry.
            }

            // If this redirect's TARGET points back to the new redirect's SOURCE, adding
            // the new redirect would close a cycle in the chain.
            if (TargetMatchesDtoSource(current, dto))
            {
                throw new RedirectCycleException(
                    $"Adding this redirect would create a cycle (detected after {hop + 1} hop(s)).");
            }

            current = await FindNextInChainAsync(current);
        }
    }

    private async Task<Redirect?> FindRedirectByTargetAsync(RedirectCreateDto dto)
    {
        return dto.TargetType switch
        {
            RedirectTargetType.InternalPath when dto.ToPath != null =>
                await dbContext.Set<Redirect>()
                    .FirstOrDefaultAsync(r =>
                        r.SourceType == RedirectSourceType.InternalPath &&
                        r.FromPath == dto.ToPath),

            RedirectTargetType.ContentSlug when dto.ToLanguage != null && dto.ToSlug != null =>
                await dbContext.Set<Redirect>()
                    .FirstOrDefaultAsync(r =>
                        r.SourceType == RedirectSourceType.ContentSlug &&
                        r.FromLanguage == dto.ToLanguage &&
                        r.FromSlug == dto.ToSlug),

            RedirectTargetType.ContentId when dto.ToContentId != null =>
                await dbContext.Set<Redirect>()
                    .FirstOrDefaultAsync(r =>
                        r.SourceType == RedirectSourceType.ContentId &&
                        r.FromContentId == dto.ToContentId),

            _ => null,
        };
    }

    private async Task<Redirect?> FindNextInChainAsync(Redirect current)
    {
        return current.TargetType switch
        {
            RedirectTargetType.InternalPath when current.ToPath != null =>
                await dbContext.Set<Redirect>()
                    .FirstOrDefaultAsync(r =>
                        r.SourceType == RedirectSourceType.InternalPath &&
                        r.FromPath == current.ToPath),

            RedirectTargetType.ContentSlug when current.ToLanguage != null && current.ToSlug != null =>
                await dbContext.Set<Redirect>()
                    .FirstOrDefaultAsync(r =>
                        r.SourceType == RedirectSourceType.ContentSlug &&
                        r.FromLanguage == current.ToLanguage &&
                        r.FromSlug == current.ToSlug),

            RedirectTargetType.ContentId when current.ToContentId != null =>
                await dbContext.Set<Redirect>()
                    .FirstOrDefaultAsync(r =>
                        r.SourceType == RedirectSourceType.ContentId &&
                        r.FromContentId == current.ToContentId),

            _ => null,
        };
    }

    private async Task<List<DiscoveryRow>> RunDiscoverySqlAsync()
    {
        const string sql = @"
            WITH ranked AS (
                SELECT
                    (data->>'id')::int AS content_id,
                    data->>'language' AS new_language,
                    data->>'slug' AS new_slug,
                    created_at,
                    LAG(data->>'language') OVER w AS old_language,
                    LAG(data->>'slug') OVER w AS old_slug
                FROM change_log
                WHERE object_type = 'Content'
                WINDOW w AS (
                    PARTITION BY (data->>'id')::int
                    ORDER BY created_at, id
                )
            ),
            redirects AS (
                SELECT
                    content_id,
                    old_language,
                    new_language,
                    old_slug,
                    new_slug,
                    created_at
                FROM ranked
                WHERE (old_language IS NOT NULL AND old_language <> new_language)
                    OR (old_slug IS NOT NULL AND old_slug <> new_slug)
            ),
            normalized_redirects AS (
                SELECT
                    content_id,
                    old_language,
                    new_language,
                    TRIM(BOTH '/' FROM old_slug) AS old_slug_trim,
                    TRIM(BOTH '/' FROM new_slug) AS new_slug_trim,
                    created_at
                FROM redirects
            ),
            final_redirects AS (
                SELECT
                    nr.*,
                    ROW_NUMBER() OVER (
                        PARTITION BY old_language, old_slug_trim
                        ORDER BY nr.created_at DESC
                    ) AS rn
                FROM normalized_redirects nr
                LEFT JOIN content c
                    ON c.language = nr.old_language
                    AND TRIM(BOTH '/' FROM c.slug) = nr.old_slug_trim
                WHERE c.id IS NULL
            )
            SELECT DISTINCT
                content_id,
                old_language,
                new_language,
                old_slug_trim AS old_slug,
                new_slug_trim AS new_slug
            FROM final_redirects
            WHERE rn = 1
            ORDER BY content_id, old_language, old_slug, new_slug";

        var results = new List<DiscoveryRow>();

        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;

        await dbContext.Database.OpenConnectionAsync();

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new DiscoveryRow
            {
                OldLanguage = reader.IsDBNull(reader.GetOrdinal("old_language")) ? null : reader.GetString(reader.GetOrdinal("old_language")),
                NewLanguage = reader.GetString(reader.GetOrdinal("new_language")),
                OldSlug = reader.IsDBNull(reader.GetOrdinal("old_slug")) ? null : reader.GetString(reader.GetOrdinal("old_slug")),
                NewSlug = reader.GetString(reader.GetOrdinal("new_slug")),
            });
        }

        return results;
    }

    private sealed class DiscoveryRow
    {
        public string? OldLanguage { get; init; }

        public string NewLanguage { get; init; } = string.Empty;

        public string? OldSlug { get; init; }

        public string NewSlug { get; init; } = string.Empty;
    }
}
