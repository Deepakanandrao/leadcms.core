// <copyright file="EmailSyncTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Entities;
using LeadCMS.Exceptions;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.EmailSync.Data;
using LeadCMS.Plugin.EmailSync.Entities;
using LeadCMS.Services;
using LeadCMS.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Serilog;

namespace LeadCMS.EmailSync.Tasks
{
    public class EmailSyncTask : BaseTask
    {
        internal static readonly string[] DefaultIgnoredFolderKeywords = new[]
        {
            "Spam",
            "Junk",
            "Draft",
            "Archive",
            "Deleted",
            "Trash",
            "Bin",
            "Starred",
            "Important",
            "Flagged",
            "Bulk",
            "Clutter",
            "Conversation History",
            "Notes",
            "Calendar",
            "Contacts",
            "Tasks",
        };

        private readonly EmailSyncDbContext dbContext;

        private readonly int batchSize;

        private readonly string[] internalDomains;

        private readonly string[] ignoredEmails;

        private readonly string[] whitelistedFolders;

        private readonly string[] ignoredFolderKeywords;

        private readonly IDomainService domainService;
        private readonly IContactService contactsService;

        public EmailSyncTask(IConfiguration configuration, EmailSyncDbContext dbContext, TaskStatusService taskStatusService, IDomainService domainService, IContactService contactsService)
            : base("Tasks:EmailSyncTask", configuration, taskStatusService)
        {
            this.dbContext = dbContext;
            this.domainService = domainService;
            this.contactsService = contactsService;

            var config = configuration.GetSection(configKey)!.Get<TaskWithBatchConfig>();
            if (config is not null)
            {
                batchSize = config.BatchSize;
            }
            else
            {
                throw new MissingConfigurationException($"The specified configuration section for the provided configKey {configKey} could not be found in the settings file.");
            }

            var domains = configuration.GetSection("EmailSync:InternalDomains")!.Get<string[]>();
            internalDomains = (domains != null) ? domains : new string[0];

            var ignored = configuration.GetSection("EmailSync:IgnoredEmails")!.Get<string[]>();
            ignoredEmails = (ignored != null) ? ignored : new string[0];

            var whitelist = configuration.GetSection("EmailSync:WhitelistedFolders")!.Get<string[]>();
            whitelistedFolders = (whitelist != null) ? whitelist : new string[0];

            var folderKeywords = configuration.GetSection("EmailSync:IgnoredFolderKeywords")!.Get<string[]>();
            ignoredFolderKeywords = GetIgnoredFolderKeywords(folderKeywords);

            domainService.SetDBContext(dbContext);
            contactsService.SetDBContext(dbContext);
        }

        public override async Task<bool> Execute(TaskExecutionLog currentJob)
        {
            try
            {
                var accounts = dbContext.ImapAccounts!.OrderBy(ia => ia.Id).ToList();
                var totalAccounts = accounts.Count;
                var successfulAccounts = 0;
                var failedAccounts = 0;
                var syncedFolders = 0;
                var skippedFolders = 0;
                var removedFolders = 0;
                var importedEmails = 0;
                var createdContacts = 0;

                foreach (var imapAccount in accounts)
                {
                    try
                    {
                        using (var client = new ImapClient())
                        {
                            client.Connect(imapAccount.Host, imapAccount.Port, imapAccount.UseSsl);

                            client.Authenticate(imapAccount.UserName, imapAccount.Password);

                            foreach (var personalNamespace in client.PersonalNamespaces)
                            {
                                var folders = client.GetFolders(personalNamespace);

                                var imapAccountFolders = dbContext.ImapAccountFolders!.Where(f => f.ImapAccountId == imapAccount.Id).ToList();

                                foreach (var folder in folders)
                                {
                                    if (!ShouldSyncFolder(folder.FullName, whitelistedFolders, ignoredFolderKeywords))
                                    {
                                        skippedFolders++;
                                        continue;
                                    }

                                    var summary = await GetEmailLogsFromFolder(imapAccount.UserName, imapAccountFolders, folder, imapAccount);
                                    syncedFolders++;
                                    importedEmails += summary.ImportedEmails;
                                    createdContacts += summary.CreatedContacts;
                                }

                                removedFolders += await DeleteUnexistedFolders(imapAccountFolders, folders);
                            }

                            successfulAccounts++;
                        }
                    }
                    catch (Exception e)
                    {
                        failedAccounts++;
                        Log.Error(e, $"Error occured during imap syncronization, imap: {imapAccount.Host}, userName: {imapAccount.UserName}");
                    }
                }

                currentJob.Result = $"Processed {totalAccounts} IMAP accounts ({successfulAccounts} successful, {failedAccounts} failed), synced {syncedFolders} folders, skipped {skippedFolders} folders, imported {importedEmails} emails, created {createdContacts} contacts, removed {removedFolders} stale folders";
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to execute {Name} in task runner {currentJob.Id}");
                currentJob.Result = $"Email sync failed: {ex.Message}";
                return false;
            }
        }

        internal static bool IsFolderIgnored(string folderFullName, string[] keywords)
        {
            if (keywords.Length == 0)
            {
                return false;
            }

            return Array.Exists(keywords, keyword =>
                folderFullName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        internal static string[] GetIgnoredFolderKeywords(string[]? additionalKeywords)
        {
            if (additionalKeywords == null || additionalKeywords.Length == 0)
            {
                return DefaultIgnoredFolderKeywords;
            }

            return DefaultIgnoredFolderKeywords
                .Concat(additionalKeywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static bool IsFolderWhitelisted(string folderFullName, string[] folders)
        {
            if (folders.Length == 0)
            {
                return true;
            }

            return folders
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .Any(folder => FolderMatchesWhitelist(folderFullName, folder.Trim()));
        }

        internal static bool ShouldSyncFolder(string folderFullName, string[] whitelistedFolders, string[] ignoredKeywords)
        {
            return IsFolderWhitelisted(folderFullName, whitelistedFolders)
                && !IsFolderIgnored(folderFullName, ignoredKeywords);
        }

        private static bool FolderMatchesWhitelist(string folderFullName, string whitelistedFolder)
        {
            if (folderFullName.Equals(whitelistedFolder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (folderFullName.Length <= whitelistedFolder.Length)
            {
                return false;
            }

            if (!folderFullName.StartsWith(whitelistedFolder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var separator = folderFullName[whitelistedFolder.Length];
            return separator == '/' || separator == '\\' || separator == '.';
        }

        private async Task<int> DeleteUnexistedFolders(List<ImapAccountFolder> imapAccountFolders, IList<IMailFolder> folders)
        {
            var existingFolderNames = folders.Select(f => f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var foldersToDelete = imapAccountFolders.Where(iaf => !existingFolderNames.Contains(iaf.FullName)).ToList();

            if (foldersToDelete.Count == 0)
            {
                return 0;
            }

            dbContext.ImapAccountFolders!.RemoveRange(foldersToDelete);
            await dbContext.SaveChangesAsync();
            return foldersToDelete.Count;
        }

        private async Task<EmailSyncSummary> GetEmailLogsFromFolder(string userName, List<ImapAccountFolder> imapAccountFolders, IMailFolder folder, ImapAccount imapAccount)
        {
            await folder.OpenAsync(FolderAccess.ReadOnly);

            var summary = new EmailSyncSummary();

            var dbFolder = imapAccountFolders.FirstOrDefault(f => f.FullName == folder.FullName);

            if (dbFolder == null)
            {
                dbFolder = new ImapAccountFolder
                {
                    FullName = folder.FullName,
                    LastUid = 0,
                    ImapAccountId = imapAccount.Id,
                    Source = userName,
                };

                await dbContext.ImapAccountFolders!.AddAsync(dbFolder);
            }

            if (folder.UidNext.HasValue && folder.UidNext.Value.Id <= dbFolder.LastUid)
            {
                dbFolder.LastUid = 0;
            }

            var range = new UniqueIdRange(new UniqueId((uint)dbFolder.LastUid + 1), UniqueId.MaxValue);
            var uids = folder.Search(range, SearchQuery.All);
            var position = 0;
            while (position < uids.Count)
            {
                var batch = uids.Skip(position).Take(batchSize);
                var batchSummary = await GetEmailLogs(userName, dbFolder, folder, batch);
                summary.ImportedEmails += batchSummary.ImportedEmails;
                summary.CreatedContacts += batchSummary.CreatedContacts;
                position += batchSize;
            }

            return summary;
        }

        private async Task<EmailSyncSummary> GetEmailLogs(string userName, ImapAccountFolder dbFolder, IMailFolder folder, IEnumerable<UniqueId> uids)
        {
            var emailLogs = new List<EmailLog>();
            var resultLastId = dbFolder.LastUid;

            var messages = new List<MimeMessage>();
            foreach (var uid in uids)
            {
                if (uid.Id <= dbFolder.LastUid)
                {
                    continue;
                }

                messages.Add(folder.GetMessage(uid));
                resultLastId = (int)uid.Id;
            }

            var existedMessagesUids = dbContext.EmailLogs!.Where(el => messages.Select(m => m.MessageId).Contains(el.MessageId)).Select(m => m.MessageId).ToList();

            foreach (var message in messages)
            {
                if (string.IsNullOrEmpty(message.MessageId))
                {
                    continue;
                }

                if (!existedMessagesUids.Contains(message.MessageId))
                {
                    var fromMailbox = message.From.Mailboxes.FirstOrDefault();

                    if (fromMailbox == null)
                    {
                        continue;
                    }

                    var fromEmail = fromMailbox.Address;

                    if (!ignoredEmails.Contains(fromEmail))
                    {
                        var recipients = message.GetRecipients().Select(r => r.Address).ToList();

                        if (!IsInternalEmails(fromEmail, recipients))
                        {
                            var from = message.From.Mailboxes.Single().Address;
                            var status = IsInternalDomain(from) ? EmailStatus.Sent : EmailStatus.Received;

                            var emailLog = new EmailLog
                            {
                                Subject = message.Subject == null ? string.Empty : message.Subject,
                                Recipients = string.Join(";", recipients),
                                FromEmail = from,
                                HtmlBody = message.HtmlBody,
                                TextBody = message.TextBody,
                                MessageId = message.MessageId,
                                Source = userName + " - " + folder.FullName,
                                Status = status,
                                CreatedAt = message.Date.UtcDateTime,
                            };

                            emailLogs.Add(emailLog);
                        }
                    }
                }
            }

            if (emailLogs.Count > 0)
            {
                var createdContacts = await EnrichWithContactIdAsync(emailLogs);

                await dbContext.EmailLogs!.AddRangeAsync(emailLogs);

                dbFolder.LastUid = resultLastId;

                await dbContext.SaveChangesAsync();

                return new EmailSyncSummary
                {
                    ImportedEmails = emailLogs.Count,
                    CreatedContacts = createdContacts,
                };
            }

            dbFolder.LastUid = resultLastId;

            await dbContext.SaveChangesAsync();

            return new EmailSyncSummary();
        }

        private async Task<int> EnrichWithContactIdAsync(List<EmailLog> emailLogs)
        {
            var emails = emailLogs
                .SelectMany(emailLog => new[] { emailLog.FromEmail }
                    .Concat(emailLog.Recipients.Split(';')))
                .Distinct()
                .Where(email => !IsInternalDomain(email) && !ignoredEmails.Contains(email))
                .ToList();

            var existingContacts = await dbContext.Contacts!
                                    .Where(contact => contact.Email != null && emails.Contains(contact.Email))
                                    .ToDictionaryAsync(contact => contact.Email!, contact => contact);

            var newContacts = new List<Contact>();

            foreach (var emailLog in emailLogs)
            {
                if (emailLog.ContactId > 0)
                {
                    continue;
                }

                var participants = new List<string> { emailLog.FromEmail };
                participants.AddRange(emailLog.Recipients.Split(';'));
                var contactEmail = participants.FirstOrDefault(email => !IsInternalDomain(email) && !ignoredEmails.Contains(email));

                if (string.IsNullOrEmpty(contactEmail))
                {
                    continue;
                }

                Contact? contact;

                if (!existingContacts.TryGetValue(contactEmail, out contact))
                {
                    contact = new Contact { Email = contactEmail };
                    newContacts.Add(contact);
                    existingContacts[contact.Email!] = contact;
                }

                emailLog.Contact = contact;
            }

            await contactsService.SaveRangeAsync(newContacts);
            return newContacts.Count;
        }

        private bool IsInternalDomain(string email)
        {
            return internalDomains.Contains(domainService.GetDomainNameByEmail(email));
        }

        private bool IsInternalEmails(string fromEmail, List<string> toEmails)
        {
            return IsInternalDomain(fromEmail) && toEmails.TrueForAll(email => IsInternalDomain(email));
        }

        private sealed class EmailSyncSummary
        {
            public int ImportedEmails { get; set; }

            public int CreatedContacts { get; set; }
        }
    }
}