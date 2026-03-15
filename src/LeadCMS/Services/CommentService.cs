// <copyright file="CommentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

public class CommentService : ICommentService
{
    private readonly IContactService contactsService;
    private PgDbContext pgDbContext;

    public CommentService(PgDbContext pgDbContext, IContactService contactsService)
    {
        this.pgDbContext = pgDbContext;
        this.contactsService = contactsService;
    }

    public async Task SaveAsync(Comment comment)
    {
        await EnrichWithContactId(comment);

        if (comment.Id > 0)
        {
            pgDbContext.Comments!.Update(comment);
        }
        else
        {
            EnsureTranslationKey(comment);
            await pgDbContext.Comments!.AddAsync(comment);
        }
    }

    public async Task SaveRangeAsync(List<Comment> comments)
    {
        await EnrichWithContactIdAsync(comments);

        var newAndExisting = comments.GroupBy(c => c.Id > 0);

        foreach (var group in newAndExisting)
        {
            if (group.Key)
            {
                pgDbContext.UpdateRange(group.ToList());
            }
            else
            {
                foreach (var comment in group)
                {
                    EnsureTranslationKey(comment);
                }

                await pgDbContext.AddRangeAsync(group.ToList());
            }
        }
    }

    public void SetDBContext(PgDbContext pgDbContext)
    {
        this.pgDbContext = pgDbContext;
        contactsService.SetDBContext(pgDbContext);
    }

    internal static void EnsureTranslationKey(Comment comment)
    {
        if (!string.IsNullOrEmpty(comment.TranslationKey))
        {
            return;
        }

        var createdAt = comment.CreatedAt == default ? DateTime.UtcNow : comment.CreatedAt;
        var createdAtHash = ComputeShortHash(createdAt.ToString("O"));
        var bodyHash = ComputeShortHash(comment.Body ?? string.Empty);
        var contentType = string.IsNullOrEmpty(comment.CommentableType) ? "general" : comment.CommentableType.ToLowerInvariant();

        comment.TranslationKey = $"comment_{contentType}_{comment.CommentableId}_{createdAtHash}_{bodyHash}";
    }

    private static string ComputeShortHash(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    private async Task EnrichWithContactId(Comment comment)
    {
        if (comment.ContactId == 0)
        {
            var contact = await pgDbContext.Contacts!.FirstOrDefaultAsync(contact => contact.Email == comment.AuthorEmail);

            if (contact == null)
            {
                contact = new Contact { Email = comment.AuthorEmail, FirstName = comment.AuthorName };

                await contactsService.SaveAsync(contact);
            }

            comment.Contact = contact;
        }
    }

    private async Task EnrichWithContactIdAsync(List<Comment> comments)
    {
        var emails = (from comment in comments
                      select comment.AuthorEmail).Distinct();

        var existingContacts = await pgDbContext.Contacts!
                                .Where(contact => contact.Email != null && emails.Contains(contact.Email))
                                .ToDictionaryAsync(contact => contact.Email!, contact => contact);

        var newContacts = new List<Contact>();

        foreach (var comment in comments)
        {
            if (comment.ContactId > 0)
            {
                continue;
            }

            Contact? contact;

            if (!existingContacts.TryGetValue(comment.AuthorEmail, out contact))
            {
                contact = new Contact { Email = comment.AuthorEmail, FirstName = comment.AuthorName };
                newContacts.Add(contact);

                if (!string.IsNullOrWhiteSpace(contact.Email))
                {
                    existingContacts[contact.Email] = contact;
                }
            }

            comment.Contact = contact;
        }

        await contactsService.SaveRangeAsync(newContacts);
    }
}