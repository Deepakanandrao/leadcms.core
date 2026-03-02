// <copyright file="CampaignService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

using static LeadCMS.Helpers.TemplateArgumentsBuilder;

namespace LeadCMS.Services;

public class CampaignService : ICampaignService
{
    private readonly PgDbContext dbContext;
    private readonly ISegmentService segmentService;
    private readonly IEmailFromTemplateService emailFromTemplateService;
    private readonly IEmailTemplateService emailTemplateService;

    public CampaignService(
        PgDbContext dbContext,
        ISegmentService segmentService,
        IEmailFromTemplateService emailFromTemplateService,
        IEmailTemplateService emailTemplateService)
    {
        this.dbContext = dbContext;
        this.segmentService = segmentService;
        this.emailFromTemplateService = emailFromTemplateService;
        this.emailTemplateService = emailTemplateService;
    }

    /// <inheritdoc/>
    public DateTime ConvertScheduledToUtc(DateTime scheduledAt, int timeZoneOffsetMinutes)
    {
        return CampaignScheduleHelper.ConvertScheduledLocalToUtc(scheduledAt, timeZoneOffsetMinutes);
    }

    /// <inheritdoc/>
    public async Task<CampaignPreviewResultDto> PreviewAsync(CampaignPreviewRequestDto dto)
    {
        var template = await dbContext.EmailTemplates!.FindAsync(dto.EmailTemplateId)
            ?? throw new KeyNotFoundException($"Email template with id {dto.EmailTemplateId} not found.");

        var templatePreviewRequest = new EmailTemplatePreviewRequestDto
        {
            Subject = template.Subject,
            BodyTemplate = template.BodyTemplate,
            FromEmail = template.FromEmail,
            FromName = template.FromName,
            ContactId = dto.ContactId,
            ContactType = dto.ContactType,
            Language = dto.Language,
            CustomTemplateParameters = dto.CustomTemplateParameters,
        };

        var segmentIds = dto.SegmentIds ?? Array.Empty<int>();
        var hasAudienceSegments = segmentIds.Length > 0;

        // Resolve audience and unsubscribed contacts sequentially (DbContext is not thread-safe).
        var allContacts = hasAudienceSegments
            ? await ResolveAudienceContactsAsync(segmentIds, dto.ExcludeSegmentIds)
            : new Dictionary<int, Contact>();

        var unsubscribedSet = await GetUnsubscribedContactIdsAsync();

        // If no specific contact was requested, pick one from the audience
        if (!dto.ContactId.HasValue && hasAudienceSegments)
        {
            var audienceContact = allContacts.Values
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email)
                    && !unsubscribedSet.Contains(c.Id)
                    && c.UnsubscribeId == null);

            if (audienceContact != null)
            {
                templatePreviewRequest.ContactId = audienceContact.Id;
            }
        }

        var templatePreview = await emailTemplateService.PreviewAsync(templatePreviewRequest);

        var (sendableCount, unsubscribedCount, invalidEmailCount) = CalculateAudienceBreakdown(allContacts, unsubscribedSet);

        return new CampaignPreviewResultDto
        {
            TotalAudienceCount = allContacts.Count,
            SendableCount = sendableCount,
            UnsubscribedCount = unsubscribedCount,
            InvalidEmailCount = invalidEmailCount,
            TemplatePreview = templatePreview,
        };
    }

    public async Task<Campaign> LaunchAsync(int campaignId, CampaignLaunchDto launchDto)
    {
        var campaign = await dbContext.Campaigns!.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign with id {campaignId} not found.");

        if (campaign.Status != CampaignStatus.Draft)
        {
            throw new InvalidOperationException($"Campaign can only be launched from Draft status. Current status: {campaign.Status}.");
        }

        // Validate template exists
        _ = await dbContext.EmailTemplates!.FindAsync(campaign.EmailTemplateId)
            ?? throw new InvalidOperationException($"Email template with id {campaign.EmailTemplateId} not found.");

        // Validate at least one segment
        if (campaign.SegmentIds == null || campaign.SegmentIds.Length == 0)
        {
            throw new InvalidOperationException("Campaign must have at least one segment.");
        }

        // Apply timezone settings from launch DTO (override campaign values if provided)
        if (launchDto.TimeZone.HasValue)
        {
            campaign.TimeZone = launchDto.TimeZone;
        }

        if (launchDto.UseContactTimeZone)
        {
            campaign.UseContactTimeZone = true;
        }

        if (launchDto.SendNow)
        {
            campaign.Status = CampaignStatus.Sending;
            campaign.SendStartedAt = DateTime.UtcNow;

            // Resolve audience immediately
            var recipientCount = await ResolveAudienceAsync(campaign);
            campaign.TotalRecipients = recipientCount;
        }
        else
        {
            var scheduledAt = launchDto.ScheduledAt ?? campaign.ScheduledAt;
            if (scheduledAt == null)
            {
                throw new InvalidOperationException("Scheduled time must be provided when not sending immediately.");
            }

            // For future-time validation: if UseContactTimeZone, use the earliest possible
            // UTC interpretation (UTC+14 = 840 min offset) so we validate against the
            // earliest timezone that will receive it.
            int offsetForValidation;
            if (campaign.UseContactTimeZone)
            {
                offsetForValidation = 840; // UTC+14 (earliest timezone)
            }
            else
            {
                offsetForValidation = campaign.TimeZone ?? 0;
            }

            var scheduledUtc = ConvertScheduledToUtc(scheduledAt.Value, offsetForValidation);

            if (scheduledUtc <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Scheduled time must be in the future.");
            }

            campaign.Status = CampaignStatus.Scheduled;
            campaign.ScheduledAt = scheduledAt;
        }

        await dbContext.SaveChangesAsync();
        return campaign;
    }

    public async Task<Campaign> CancelAsync(int campaignId)
    {
        var campaign = await dbContext.Campaigns!.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign with id {campaignId} not found.");

        if (campaign.Status != CampaignStatus.Scheduled)
        {
            throw new InvalidOperationException($"Only scheduled campaigns can be cancelled. Current status: {campaign.Status}.");
        }

        campaign.Status = CampaignStatus.Cancelled;
        await dbContext.SaveChangesAsync();
        return campaign;
    }

    public async Task<Campaign> PauseAsync(int campaignId)
    {
        var campaign = await dbContext.Campaigns!.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign with id {campaignId} not found.");

        if (campaign.Status != CampaignStatus.Sending)
        {
            throw new InvalidOperationException($"Only sending campaigns can be paused. Current status: {campaign.Status}.");
        }

        campaign.Status = CampaignStatus.Paused;
        await dbContext.SaveChangesAsync();
        return campaign;
    }

    public async Task<Campaign> ResumeAsync(int campaignId)
    {
        var campaign = await dbContext.Campaigns!.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign with id {campaignId} not found.");

        if (campaign.Status != CampaignStatus.Paused)
        {
            throw new InvalidOperationException($"Only paused campaigns can be resumed. Current status: {campaign.Status}.");
        }

        campaign.Status = CampaignStatus.Sending;
        await dbContext.SaveChangesAsync();
        return campaign;
    }

    public async Task<int> ResolveAudienceAsync(Campaign campaign)
    {
        var allContacts = await ResolveAudienceContactsAsync(campaign.SegmentIds, campaign.ExcludeSegmentIds);
        var unsubscribedSet = await GetUnsubscribedContactIdsAsync();

        // Check for existing recipients (idempotency guard for re-resolution)
        var existingContactIds = await dbContext.CampaignRecipients!
            .Where(r => r.CampaignId == campaign.Id)
            .Select(r => r.ContactId)
            .ToListAsync();
        var existingSet = new HashSet<int>(existingContactIds);

        int created = 0;
        foreach (var contact in allContacts.Values)
        {
            // Skip if already a recipient (idempotency)
            if (existingSet.Contains(contact.Id))
            {
                continue;
            }

            var recipient = new CampaignRecipient
            {
                CampaignId = campaign.Id,
                ContactId = contact.Id,
            };

            // Check unsubscribe
            if (unsubscribedSet.Contains(contact.Id) || contact.UnsubscribeId != null)
            {
                recipient.Status = CampaignRecipientStatus.Skipped;
                recipient.SkipReason = CampaignSkipReason.Unsubscribed;
            }

            // Check valid email
            else if (string.IsNullOrWhiteSpace(contact.Email))
            {
                recipient.Status = CampaignRecipientStatus.Skipped;
                recipient.SkipReason = CampaignSkipReason.InvalidEmail;
            }
            else
            {
                recipient.Status = CampaignRecipientStatus.Pending;
            }

            await dbContext.CampaignRecipients!.AddAsync(recipient);
            created++;
        }

        await dbContext.SaveChangesAsync();

        return allContacts.Count;
    }

    public async Task<(int sent, int failed, int skipped)> ProcessRecipientsAsync(Campaign campaign, int batchSize = 100)
    {
        int sent = 0;
        int failed = 0;
        int skipped = 0;

        // Get the template info
        var template = await dbContext.EmailTemplates!.FindAsync(campaign.EmailTemplateId);
        if (template == null)
        {
            throw new InvalidOperationException($"Email template with id {campaign.EmailTemplateId} not found.");
        }

        // Get pending recipients in batches
        var pendingRecipients = await dbContext.CampaignRecipients!
            .Include(r => r.Contact)
                .ThenInclude(c => c!.Account)
            .Include(r => r.Contact)
                .ThenInclude(c => c!.Domain)
            .Where(r => r.CampaignId == campaign.Id && r.Status == CampaignRecipientStatus.Pending)
            .OrderBy(r => r.Id)
            .Take(batchSize)
            .ToListAsync();

        foreach (var recipient in pendingRecipients)
        {
            // Re-check campaign status in case it was paused
            var currentCampaign = await dbContext.Campaigns!.FindAsync(campaign.Id);
            if (currentCampaign!.Status == CampaignStatus.Paused || currentCampaign.Status == CampaignStatus.Cancelled)
            {
                break;
            }

            // Per-contact timezone: skip recipients whose local scheduled time has not arrived yet
            if (campaign.UseContactTimeZone && campaign.ScheduledAt.HasValue && recipient.Contact != null)
            {
                var recipientUtcTime = CampaignScheduleHelper.GetExpectedSendAtUtc(campaign, recipient.Contact);
                if (recipientUtcTime.HasValue && recipientUtcTime.Value > DateTime.UtcNow)
                {
                    continue;
                }
            }

            // Defensive: skip contact whose email was cleared between audience resolution and batch send
            if (string.IsNullOrWhiteSpace(recipient.Contact?.Email))
            {
                recipient.Status = CampaignRecipientStatus.Skipped;
                recipient.SkipReason = CampaignSkipReason.InvalidEmail;
                skipped++;
                await dbContext.SaveChangesAsync();
                continue;
            }

            try
            {
                var templateArgs = FromContact(recipient.Contact);

                await emailFromTemplateService.SendAsync(
                    template.Name,
                    template.Language,
                    new[] { recipient.Contact.Email },
                    templateArgs,
                    attachments: null,
                    contactId: recipient.ContactId,
                    campaignId: campaign.Id);

                recipient.Status = CampaignRecipientStatus.Sent;
                recipient.SentAt = DateTime.UtcNow;
                sent++;
            }
            catch (Exception ex)
            {
                recipient.Status = CampaignRecipientStatus.Failed;
                recipient.ErrorMessage = ex.Message;
                failed++;
            }

            await dbContext.SaveChangesAsync();
        }

        // Update campaign counters
        await RefreshCampaignCountersAsync(campaign.Id);

        return (sent, failed, skipped);
    }

    public async Task<CampaignStatisticsDto> GetStatisticsAsync(int campaignId)
    {
        var campaign = await dbContext.Campaigns!.FindAsync(campaignId)
            ?? throw new KeyNotFoundException($"Campaign with id {campaignId} not found.");

        var recipients = await dbContext.CampaignRecipients!
            .Where(r => r.CampaignId == campaignId)
            .ToListAsync();

        return new CampaignStatisticsDto
        {
            TotalRecipients = campaign.TotalRecipients,
            SentCount = recipients.Count(r => r.Status == CampaignRecipientStatus.Sent),
            FailedCount = recipients.Count(r => r.Status == CampaignRecipientStatus.Failed),
            SkippedCount = recipients.Count(r => r.Status == CampaignRecipientStatus.Skipped),
            PendingCount = recipients.Count(r => r.Status == CampaignRecipientStatus.Pending),
            SkippedUnsubscribed = recipients.Count(r => r.Status == CampaignRecipientStatus.Skipped && r.SkipReason == CampaignSkipReason.Unsubscribed),
            SkippedDuplicate = recipients.Count(r => r.Status == CampaignRecipientStatus.Skipped && r.SkipReason == CampaignSkipReason.Duplicate),
            SkippedSuppressed = recipients.Count(r => r.Status == CampaignRecipientStatus.Skipped && r.SkipReason == CampaignSkipReason.Suppressed),
            SkippedInvalidEmail = recipients.Count(r => r.Status == CampaignRecipientStatus.Skipped && r.SkipReason == CampaignSkipReason.InvalidEmail),
        };
    }

    private static (int sendable, int unsubscribed, int invalidEmail) CalculateAudienceBreakdown(
        Dictionary<int, Contact> contacts,
        HashSet<int> unsubscribedSet)
    {
        int unsubscribedCount = 0;
        int invalidEmailCount = 0;
        int sendableCount = 0;

        foreach (var contact in contacts.Values)
        {
            if (unsubscribedSet.Contains(contact.Id) || contact.UnsubscribeId != null)
            {
                unsubscribedCount++;
            }
            else if (string.IsNullOrWhiteSpace(contact.Email))
            {
                invalidEmailCount++;
            }
            else
            {
                sendableCount++;
            }
        }

        return (sendableCount, unsubscribedCount, invalidEmailCount);
    }

    /// <summary>
    /// Resolves the target audience by unioning contacts from included segments
    /// and removing contacts from excluded segments.
    /// </summary>
    private async Task<Dictionary<int, Contact>> ResolveAudienceContactsAsync(int[] segmentIds, int[]? excludeSegmentIds)
    {
        var allContacts = new Dictionary<int, Contact>();

        foreach (var segmentId in segmentIds)
        {
            var contacts = await segmentService.GetSegmentContactsAsync(segmentId);
            foreach (var contact in contacts)
            {
                allContacts.TryAdd(contact.Id, contact);
            }
        }

        if (excludeSegmentIds != null)
        {
            foreach (var segmentId in excludeSegmentIds)
            {
                var excludedContacts = await segmentService.GetSegmentContactsAsync(segmentId);
                foreach (var contact in excludedContacts)
                {
                    allContacts.Remove(contact.Id);
                }
            }
        }

        return allContacts;
    }

    /// <summary>
    /// Loads the set of globally unsubscribed contact IDs.
    /// </summary>
    private async Task<HashSet<int>> GetUnsubscribedContactIdsAsync()
    {
        var unsubscribedContactIds = await dbContext.Unsubscribes!
            .Where(u => u.ContactId != null)
            .Select(u => u.ContactId!.Value)
            .ToListAsync();
        return new HashSet<int>(unsubscribedContactIds);
    }

    private async Task RefreshCampaignCountersAsync(int campaignId)
    {
        var campaign = await dbContext.Campaigns!.FindAsync(campaignId);
        if (campaign == null)
        {
            return;
        }

        var recipients = await dbContext.CampaignRecipients!
            .Where(r => r.CampaignId == campaignId)
            .ToListAsync();

        campaign.SentCount = recipients.Count(r => r.Status == CampaignRecipientStatus.Sent);
        campaign.FailedCount = recipients.Count(r => r.Status == CampaignRecipientStatus.Failed);
        campaign.SkippedCount = recipients.Count(r => r.Status == CampaignRecipientStatus.Skipped);

        await dbContext.SaveChangesAsync();
    }
}
