// <copyright file="RedirectService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.DTOs;
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

    public async Task<List<RedirectDetailsDto>> DiscoverRedirectsAsync()
    {        
        var sql = $@"
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

        var results = new List<RedirectDetailsDto>();

        using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;        
        
        await dbContext.Database.OpenConnectionAsync();
        
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            results.Add(new RedirectDetailsDto
            {
                ContentId = reader.GetInt32(reader.GetOrdinal("content_id")),
                FromLanguage = reader.IsDBNull(reader.GetOrdinal("old_language")) ? string.Empty : reader.GetString(reader.GetOrdinal("old_language")),
                ToLanguage = reader.GetString(reader.GetOrdinal("new_language")),
                FromSlug = reader.IsDBNull(reader.GetOrdinal("old_slug")) ? string.Empty : reader.GetString(reader.GetOrdinal("old_slug")),
                ToSlug = reader.GetString(reader.GetOrdinal("new_slug")),
            });
        }

        return results;
    }
}
