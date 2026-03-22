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

    /// <summary>
    /// Loads the most recent sent and received <see cref="EmailLog"/> entries for a single contact.
    /// Used to populate email-history template parameters without loading the full log collection.
    /// </summary>
    public static async Task<(EmailLog? LastSent, EmailLog? LastReceived)> LoadLastEmailLogsAsync(
        PgDbContext dbContext, int contactId)
    {
        var lastSent = await dbContext.EmailLogs!
            .AsNoTracking()
            .Where(e => e.ContactId == contactId && e.Status == EmailStatus.Sent)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        var lastReceived = await dbContext.EmailLogs!
            .AsNoTracking()
            .Where(e => e.ContactId == contactId && e.Status == EmailStatus.Received)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        return (lastSent, lastReceived);
    }

    /// <summary>
    /// Loads the most recent sent and received <see cref="EmailLog"/> entries for multiple contacts in batch.
    /// Returns a dictionary keyed by contact ID containing the last sent and received logs.
    /// </summary>
    public static async Task<Dictionary<int, (EmailLog? LastSent, EmailLog? LastReceived)>> LoadLastEmailLogsBatchAsync(
        PgDbContext dbContext, IReadOnlyCollection<int> contactIds)
    {
        if (contactIds.Count == 0)
        {
            return new Dictionary<int, (EmailLog?, EmailLog?)>();
        }

        var lastSentLogs = await dbContext.EmailLogs!
            .AsNoTracking()
            .Where(e => e.ContactId.HasValue && contactIds.Contains(e.ContactId.Value) && e.Status == EmailStatus.Sent)
            .GroupBy(e => e.ContactId!.Value)
            .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
            .ToListAsync();

        var lastReceivedLogs = await dbContext.EmailLogs!
            .AsNoTracking()
            .Where(e => e.ContactId.HasValue && contactIds.Contains(e.ContactId.Value) && e.Status == EmailStatus.Received)
            .GroupBy(e => e.ContactId!.Value)
            .Select(g => g.OrderByDescending(e => e.CreatedAt).First())
            .ToListAsync();

        var sentByContact = lastSentLogs.ToDictionary(e => e.ContactId!.Value);
        var receivedByContact = lastReceivedLogs.ToDictionary(e => e.ContactId!.Value);

        return contactIds.Distinct().ToDictionary(
            id => id,
            id => (
                sentByContact.GetValueOrDefault(id),
                receivedByContact.GetValueOrDefault(id)));
    }
}