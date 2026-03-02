// <copyright file="EmailWithLogService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services
{
    public class EmailWithLogService : IEmailWithLogService
    {
        private readonly IEmailService emailService;
        private readonly PgDbContext pgDbContext;

        public EmailWithLogService(IEmailService emailService, PgDbContext pgDbContext)
        {
            this.emailService = emailService;
            this.pgDbContext = pgDbContext;
        }

        public async Task SendAsync(string subject, string fromEmail, string fromName, string[] recipients, string body, List<AttachmentDto>? attachments, int templateId = 0, int contactId = 0, int campaignId = 0)
        {
            var emailStatus = false;
            var emails = string.Join(";", recipients);

            string messageId = string.Empty;

            try
            {
                messageId = await emailService.SendAsync(subject, fromEmail, fromName, recipients, body, attachments);
                emailStatus = true;

                Log.Information($"Email with subject {subject} sent to {recipients} from {fromEmail}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error occurred when sending email with subject {subject} to {emails} from {fromEmail}");

                throw;
            }
            finally
            {
                await AddEmailLogEntry(subject, fromEmail, body, emails, emailStatus, messageId, contactId, templateId: templateId, campaignId: campaignId);
            }
        }

        public async Task SendToContactAsync(int contactId, string subject, string fromEmail, string fromName, string body, List<AttachmentDto>? attachments, int scheduleId = 0, int templateId = 0, int campaignId = 0)
        {
            var emailStatus = false;
            var recipient = string.Empty;

            string messageId = string.Empty;

            try
            {
                recipient = await GetContactEmailById(contactId);

                var recipientCollection = new[] { recipient };

                messageId = await emailService.SendAsync(subject, fromEmail, fromName, recipientCollection, body, attachments);
                emailStatus = true;

                Log.Information($"Email with subject {subject} sent to {recipient} from {fromEmail}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error occurred when sending email with subject {subject} to {recipient} from {fromEmail}");
                throw;
            }
            finally
            {
                await AddEmailLogEntry(subject, fromEmail, body, recipient, emailStatus, messageId, contactId, scheduleId, templateId, campaignId);
            }
        }

        private async Task AddEmailLogEntry(string subject, string fromEmail, string body, string recipient, bool status, string messageId, int contactId = 0, int scheduleId = 0, int templateId = 0, int campaignId = 0)
        {
            try
            {
                var log = new EmailLog();

                if (contactId > 0)
                {
                    log.ContactId = contactId;
                }

                if (scheduleId > 0)
                {
                    log.ScheduleId = scheduleId;
                }

                if (templateId > 0)
                {
                    log.TemplateId = templateId;
                }

                if (campaignId > 0)
                {
                    log.CampaignId = campaignId;
                }

                log.Subject = subject;
                log.FromEmail = fromEmail;
                log.HtmlBody = body;
                log.Recipients = recipient;
                log.Status = status ? EmailStatus.Sent : EmailStatus.NotSent;
                log.CreatedAt = DateTime.UtcNow;
                log.MessageId = messageId;

                await pgDbContext.EmailLogs!.AddAsync(log);
                await pgDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred when adding a email log entry.");
            }
        }

        private async Task<string> GetContactEmailById(int contactId)
        {
            var contact = await pgDbContext.Contacts!.FirstOrDefaultAsync(x => x.Id == contactId);

            if (contact == null || string.IsNullOrWhiteSpace(contact.Email))
            {
                throw new InvalidOperationException($"Contact with id {contactId} does not have an email address.");
            }

            return contact.Email;
        }
    }
}