// <copyright file="EmailGroupResolutionService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

/// <summary>
/// Resolves the target email group in a given language by matching TranslationKey or Name.
/// </summary>
public class EmailGroupResolutionService : IEmailGroupResolutionService
{
    private readonly PgDbContext dbContext;

    public EmailGroupResolutionService(PgDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<int> ResolveTargetEmailGroupIdAsync(int sourceEmailGroupId, string targetLanguage)
    {
        if (sourceEmailGroupId == 0)
        {
            return 0;
        }

        var sourceGroup = await dbContext.EmailGroups!
            .FirstOrDefaultAsync(eg => eg.Id == sourceEmailGroupId);

        if (sourceGroup == null)
        {
            return 0;
        }

        // First try matching by TranslationKey (most reliable for translated groups)
        if (!string.IsNullOrWhiteSpace(sourceGroup.TranslationKey))
        {
            var groupByTranslationKey = await dbContext.EmailGroups!
                .Where(eg => eg.TranslationKey == sourceGroup.TranslationKey && eg.Language == targetLanguage)
                .FirstOrDefaultAsync();

            if (groupByTranslationKey != null)
            {
                return groupByTranslationKey.Id;
            }
        }

        // Fallback: try matching by Name + target language
        var groupByName = await dbContext.EmailGroups!
            .Where(eg => eg.Name == sourceGroup.Name && eg.Language == targetLanguage)
            .FirstOrDefaultAsync();

        return groupByName?.Id ?? 0;
    }
}
