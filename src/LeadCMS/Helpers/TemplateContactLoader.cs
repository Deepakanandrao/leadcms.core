// <copyright file="TemplateContactLoader.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Helpers;

public static class TemplateContactLoader
{
    public static IQueryable<Contact> BuildQuery(PgDbContext dbContext)
    {
        return dbContext.Contacts!
            .Include(c => c.Account)
            .Include(c => c.Domain)
            .Include(c => c.Orders)!
                .ThenInclude(o => o.OrderItems)
            .Include(c => c.Deals)!
                .ThenInclude(d => d.DealPipeline)
            .Include(c => c.Deals)!
                .ThenInclude(d => d.DealPipelineStage);
    }

    public static Task<Contact?> LoadByIdAsync(PgDbContext dbContext, int contactId)
    {
        return BuildQuery(dbContext)
            .FirstOrDefaultAsync(c => c.Id == contactId);
    }

    public static async Task<Dictionary<int, Contact>> LoadByIdsAsync(PgDbContext dbContext, IReadOnlyCollection<int> contactIds)
    {
        if (contactIds.Count == 0)
        {
            return new Dictionary<int, Contact>();
        }

        var contacts = await BuildQuery(dbContext)
            .Where(c => contactIds.Contains(c.Id))
            .ToListAsync();

        return contacts.ToDictionary(c => c.Id);
    }
}