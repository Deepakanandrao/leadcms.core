// <copyright file="SequenceService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Core.Sequences.DTOs;
using LeadCMS.Core.Sequences.Interfaces;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using LeadCMS.Models;
using LeadCMS.Services;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.Sequences.Services;

public class SequenceService : ISequenceService
{
    private readonly PgDbContext dbContext;
    private readonly IEmailFromTemplateService emailFromTemplateService;
    private readonly IMapper mapper;
    private readonly ILiquidTemplateService liquidTemplateService;

    public SequenceService(PgDbContext dbContext, IEmailFromTemplateService emailFromTemplateService, IMapper mapper, ILiquidTemplateService liquidTemplateService)
    {
        this.dbContext = dbContext;
        this.emailFromTemplateService = emailFromTemplateService;
        this.mapper = mapper;
        this.liquidTemplateService = liquidTemplateService;
    }

    public static DateTime CalculateScheduledAt(
        DateTime baseTimeUtc,
        SequenceStepTiming timing,
        bool useContactTimeZone,
        int sequenceTimeZoneMinutes,
        int? contactTimeZoneMinutes)
    {
        var offsetMinutes = useContactTimeZone
            ? contactTimeZoneMinutes ?? sequenceTimeZoneMinutes
            : sequenceTimeZoneMinutes;

        var delay = timing.Delay;
        var scheduledUtc = delay.Unit switch
        {
            "hours" => baseTimeUtc.AddHours(delay.Value),
            "days" => baseTimeUtc.AddDays(delay.Value),
            _ => baseTimeUtc.AddMinutes(delay.Value),
        };

        if (!string.IsNullOrEmpty(timing.SendAt) && TimeSpan.TryParse(timing.SendAt, out var sendAtTime))
        {
            var localTime = scheduledUtc.AddMinutes(offsetMinutes);
            var localDate = localTime.Date;
            var targetLocal = localDate.Add(sendAtTime);

            if (localTime >= targetLocal)
            {
                targetLocal = targetLocal.AddDays(1);
            }

            if (timing.AllowedWeekDays != null && timing.AllowedWeekDays.Length > 0)
            {
                targetLocal = AdvanceToAllowedWeekDay(targetLocal, timing.AllowedWeekDays);
            }

            scheduledUtc = targetLocal.AddMinutes(-offsetMinutes);
        }
        else if (timing.AllowedWeekDays != null && timing.AllowedWeekDays.Length > 0)
        {
            var localTime = scheduledUtc.AddMinutes(offsetMinutes);
            localTime = AdvanceToAllowedWeekDay(localTime, timing.AllowedWeekDays);
            scheduledUtc = localTime.AddMinutes(-offsetMinutes);
        }

        return scheduledUtc;
    }

    public async Task<Sequence> GetFullAsync(int sequenceId)
    {
        var sequence = await dbContext.Sequences!
            .Include(s => s.Steps!.OrderBy(st => st.Position))
            .FirstOrDefaultAsync(s => s.Id == sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        return sequence;
    }

    public async Task<Sequence> SaveFullAsync(int? sequenceId, SequenceCreateDto dto)
    {
        Sequence sequence;

        if (sequenceId.HasValue)
        {
            sequence = await dbContext.Sequences!
                .Include(s => s.Steps)
                .FirstOrDefaultAsync(s => s.Id == sequenceId.Value)
                ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.Value.ToString());

            if (sequence.Status != SequenceStatus.Draft && sequence.Status != SequenceStatus.Paused)
            {
                throw new InvalidOperationException(
                    $"Sequence can only be edited in Draft or Paused status. Current status: {sequence.Status}.");
            }

            sequence.Name = dto.Name;
            sequence.Description = dto.Description;
            sequence.Language = dto.Language;
            sequence.StopOnReply = dto.StopOnReply;
            sequence.UseContactTimeZone = dto.UseContactTimeZone;
            sequence.TimeZone = dto.TimeZone;
            sequence.Enrollment = dto.Enrollment;
            sequence.UtmParameters = dto.UtmParameters;
        }
        else
        {
            sequence = new Sequence
            {
                Name = dto.Name,
                Description = dto.Description,
                Language = dto.Language,
                Status = SequenceStatus.Draft,
                StopOnReply = dto.StopOnReply,
                UseContactTimeZone = dto.UseContactTimeZone,
                TimeZone = dto.TimeZone,
                Enrollment = dto.Enrollment,
                UtmParameters = dto.UtmParameters,
            };

            await dbContext.Sequences!.AddAsync(sequence);
        }

        await ReconcileStepsAsync(sequence, dto.Steps);

        return await GetFullAsync(sequence.Id);
    }

    public async Task<Sequence> ReplaceStepsAsync(int sequenceId, List<SequenceStepCreateDto> steps)
    {
        var sequence = await dbContext.Sequences!
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.Id == sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        if (sequence.Status != SequenceStatus.Draft && sequence.Status != SequenceStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Sequence can only be edited in Draft or Paused status. Current status: {sequence.Status}.");
        }

        await ReconcileStepsAsync(sequence, steps);

        return await GetFullAsync(sequence.Id);
    }

    public async Task<Sequence> ActivateAsync(int sequenceId)
    {
        var sequence = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        if (sequence.Status != SequenceStatus.Draft && sequence.Status != SequenceStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Sequence can only be activated from Draft or Paused status. Current status: {sequence.Status}.");
        }

        var hasSteps = await dbContext.SequenceSteps!
            .AnyAsync(s => s.SequenceId == sequenceId);

        if (!hasSteps)
        {
            throw new InvalidOperationException(
                "Sequence must have at least one step before it can be activated.");
        }

        // Validate all steps have valid templates
        var stepsWithMissingTemplates = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequenceId)
            .Where(s => !dbContext.EmailTemplates!.Any(t => t.Id == s.EmailTemplateId))
            .Select(s => s.Name)
            .ToListAsync();

        if (stepsWithMissingTemplates.Any())
        {
            throw new InvalidOperationException(
                $"Steps with missing email templates: {string.Join(", ", stepsWithMissingTemplates)}.");
        }

        sequence.Status = SequenceStatus.Active;
        sequence.LastActivatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return sequence;
    }

    public async Task<Sequence> PauseAsync(int sequenceId)
    {
        var sequence = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        if (sequence.Status != SequenceStatus.Active)
        {
            throw new InvalidOperationException(
                $"Only active sequences can be paused. Current status: {sequence.Status}.");
        }

        sequence.Status = SequenceStatus.Paused;
        sequence.LastPausedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return sequence;
    }

    public async Task<Sequence> ArchiveAsync(int sequenceId)
    {
        var sequence = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        if (sequence.Status == SequenceStatus.Archived)
        {
            throw new InvalidOperationException("Sequence is already archived.");
        }

        // Exit all active enrollments
        var activeEnrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequenceId && e.Status == SequenceEnrollmentStatus.Active)
            .ToListAsync();

        foreach (var enrollment in activeEnrollments)
        {
            enrollment.Status = SequenceEnrollmentStatus.Exited;
            enrollment.ExitReason = SequenceExitReason.Archived;
            enrollment.ExitedAt = DateTime.UtcNow;
        }

        sequence.Status = SequenceStatus.Archived;
        sequence.ArchivedAt = DateTime.UtcNow;
        sequence.ActiveEnrollmentCount = 0;
        sequence.ExitedEnrollmentCount += activeEnrollments.Count;
        await dbContext.SaveChangesAsync();

        return sequence;
    }

    public async Task<SequenceStatisticsDto> GetStatisticsAsync(int sequenceId)
    {
        _ = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        var enrollmentCounts = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequenceId)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var deliveryCounts = await dbContext.SequenceDeliveries!
            .Where(d => d.SequenceId == sequenceId)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var stepsCount = await dbContext.SequenceSteps!
            .CountAsync(s => s.SequenceId == sequenceId);

        return new SequenceStatisticsDto
        {
            ActiveEnrollmentCount = enrollmentCounts
                .FirstOrDefault(c => c.Status == SequenceEnrollmentStatus.Active)?.Count ?? 0,
            CompletedEnrollmentCount = enrollmentCounts
                .FirstOrDefault(c => c.Status == SequenceEnrollmentStatus.Completed)?.Count ?? 0,
            ExitedEnrollmentCount = enrollmentCounts
                .FirstOrDefault(c => c.Status == SequenceEnrollmentStatus.Exited)?.Count ?? 0,
            SentCount = deliveryCounts
                .FirstOrDefault(c => c.Status == SequenceDeliveryStatus.Sent)?.Count ?? 0,
            FailedCount = deliveryCounts
                .FirstOrDefault(c => c.Status == SequenceDeliveryStatus.Failed)?.Count ?? 0,
            StepsCount = stepsCount,
        };
    }

    public async Task<List<SequenceEnrollment>> EnrollContactsAsync(int sequenceId, int[] contactIds, string? enrollmentReason, Dictionary<string, string>? templateArguments = null, SequenceEnrollmentSource source = SequenceEnrollmentSource.Api)
    {
        var sequence = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        if (sequence.Status != SequenceStatus.Active)
        {
            throw new InvalidOperationException(
                $"Contacts can only be enrolled in active sequences. Current status: {sequence.Status}.");
        }

        var modeName = source switch
        {
            SequenceEnrollmentSource.Manual => "manual",
            SequenceEnrollmentSource.Api => "api",
            SequenceEnrollmentSource.Segment => "segment",
            _ => source.ToString().ToLowerInvariant(),
        };

        var enrollmentConfig = sequence.Enrollment;
        if (enrollmentConfig != null && !enrollmentConfig.Modes.Contains(modeName))
        {
            throw new InvalidOperationException(
                $"Enrollment via '{modeName}' is not enabled for this sequence.");
        }

        var distinctContactIds = contactIds.Distinct().ToList();
        var contacts = await dbContext.Contacts!
            .Where(c => distinctContactIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);
        var unsubscribedIds = await GetUnsubscribedContactIdsAsync();

        var enrollments = new List<SequenceEnrollment>();

        foreach (var contactId in contactIds)
        {
            if (!contacts.TryGetValue(contactId, out var contact))
            {
                throw new EntityNotFoundException(nameof(Contact), contactId.ToString());
            }

            // Check reentry policy
            if (enrollmentConfig != null)
            {
                var existingEnrollment = await dbContext.SequenceEnrollments!
                    .Where(e => e.SequenceId == sequenceId && e.ContactId == contactId)
                    .OrderByDescending(e => e.EnteredAt)
                    .FirstOrDefaultAsync();

                if (existingEnrollment != null)
                {
                    switch (enrollmentConfig.ReentryPolicy)
                    {
                        case ReentryPolicy.OnceEver:
                            throw new InvalidOperationException(
                                $"Contact {contactId} has already been enrolled in this sequence and reentry policy is OnceEver.");
                        case ReentryPolicy.AllowAfterCompletion:
                            if (existingEnrollment.Status == SequenceEnrollmentStatus.Active)
                            {
                                throw new InvalidOperationException(
                                    $"Contact {contactId} is currently in this sequence and reentry policy is AllowAfterCompletion.");
                            }

                            break;
                        case ReentryPolicy.Always:
                            break;
                    }
                }
            }

            var isUnsubscribed = unsubscribedIds.Contains(contactId) || contact.UnsubscribeId != null;
            var enteredAt = DateTime.UtcNow;

            var enrollment = new SequenceEnrollment
            {
                SequenceId = sequenceId,
                ContactId = contactId,
                Status = isUnsubscribed ? SequenceEnrollmentStatus.Exited : SequenceEnrollmentStatus.Active,
                EnteredAt = enteredAt,
                ExitedAt = isUnsubscribed ? enteredAt : null,
                ExitReason = isUnsubscribed ? SequenceExitReason.Unsubscribed : SequenceExitReason.None,
                EnrollmentSource = source,
                EnrollmentReason = enrollmentReason,
                TemplateArguments = templateArguments,
            };

            enrollments.Add(enrollment);
        }

        await dbContext.SequenceEnrollments!.AddRangeAsync(enrollments);
        sequence.ActiveEnrollmentCount += enrollments.Count(e => e.Status == SequenceEnrollmentStatus.Active);
        sequence.ExitedEnrollmentCount += enrollments.Count(e => e.Status == SequenceEnrollmentStatus.Exited);
        await dbContext.SaveChangesAsync();

        await ProcessImmediateStepsAsync(sequence, enrollments);

        return enrollments;
    }

    public async Task<List<SequenceEnrollment>> EnrollContactBySequenceNameAsync(string sequenceName, int[] contactIds, string? enrollmentReason = null, Dictionary<string, string>? templateArguments = null, SequenceEnrollmentSource source = SequenceEnrollmentSource.Api)
    {
        var query = dbContext.Sequences!.Where(s => s.Name == sequenceName);

        var sequence = await query.FirstOrDefaultAsync()
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceName);

        return await EnrollContactsAsync(sequence.Id, contactIds, enrollmentReason, templateArguments, source);
    }

    public async Task<SequenceEnrollment> RemoveEnrollmentAsync(int sequenceId, int enrollmentId)
    {
        var enrollment = await dbContext.SequenceEnrollments!
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.SequenceId == sequenceId)
            ?? throw new EntityNotFoundException(nameof(SequenceEnrollment), enrollmentId.ToString());

        if (enrollment.Status != SequenceEnrollmentStatus.Active)
        {
            throw new InvalidOperationException(
                $"Only active enrollments can be removed. Current status: {enrollment.Status}.");
        }

        enrollment.Status = SequenceEnrollmentStatus.Exited;
        enrollment.ExitReason = SequenceExitReason.ManuallyRemoved;
        enrollment.ExitedAt = DateTime.UtcNow;

        var sequence = await dbContext.Sequences!.FindAsync(sequenceId);
        if (sequence != null)
        {
            sequence.ActiveEnrollmentCount = Math.Max(0, sequence.ActiveEnrollmentCount - 1);
            sequence.ExitedEnrollmentCount++;
        }

        await dbContext.SaveChangesAsync();

        return enrollment;
    }

    public async Task<List<SequenceEnrollment>> StopEnrollmentsAsync(int sequenceId, int[] enrollmentIds)
    {
        var enrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequenceId
                && enrollmentIds.Contains(e.Id)
                && e.Status == SequenceEnrollmentStatus.Active)
            .ToListAsync();

        if (enrollments.Count == 0)
        {
            return enrollments;
        }

        var now = DateTime.UtcNow;

        foreach (var enrollment in enrollments)
        {
            enrollment.Status = SequenceEnrollmentStatus.Exited;
            enrollment.ExitReason = SequenceExitReason.ManuallyRemoved;
            enrollment.ExitedAt = now;
        }

        var sequence = await dbContext.Sequences!.FindAsync(sequenceId);
        if (sequence != null)
        {
            sequence.ActiveEnrollmentCount = Math.Max(0, sequence.ActiveEnrollmentCount - enrollments.Count);
            sequence.ExitedEnrollmentCount += enrollments.Count;
        }

        await dbContext.SaveChangesAsync();

        return enrollments;
    }

    public async Task<SequenceEnrollmentDetailsDto> GetEnrollmentWithTimelineAsync(int sequenceId, int enrollmentId)
    {
        var enrollment = await dbContext.SequenceEnrollments!
            .Include(e => e.LastCompletedStep)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.SequenceId == sequenceId)
            ?? throw new EntityNotFoundException(nameof(SequenceEnrollment), enrollmentId.ToString());

        var sequence = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        // Load contact with related entities for template rendering
        var contact = await TemplateContactLoader.LoadByIdAsync(dbContext, enrollment.ContactId);
        enrollment.Contact = contact;

        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequenceId)
            .OrderBy(s => s.Position)
            .ToListAsync();

        var deliveries = await dbContext.SequenceDeliveries!
            .Where(d => d.SequenceEnrollmentId == enrollmentId)
            .ToListAsync();

        var deliveriesByStepId = deliveries.ToDictionary(d => d.SequenceStepId);

        // Batch-load EmailLogs for sent deliveries
        var emailLogIds = deliveries
            .Where(d => d.EmailLogId.HasValue)
            .Select(d => d.EmailLogId!.Value)
            .Distinct()
            .ToList();

        var emailLogsById = emailLogIds.Count > 0
            ? await dbContext.EmailLogs!
                .AsNoTracking()
                .Where(l => emailLogIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id)
            : new Dictionary<int, EmailLog>();

        // Batch-load EmailTemplates for steps that need preview rendering
        var templateIds = steps.Select(s => s.EmailTemplateId).Distinct().ToList();
        var templatesById = await dbContext.EmailTemplates!
            .AsNoTracking()
            .Where(t => templateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);

        // Build template arguments for preview rendering
        var templateArgs = TemplateArgumentsBuilder.FromContact(contact);

        var utmParams = UtmsBuilder.Create()
            .WithDefaults()
            .WithContext(new Utms { Campaign = sequence.Name })
            .WithOverrides(sequence.UtmParameters)
            .Build();

        TemplateArgumentsBuilder.WithUtmParameters(templateArgs, utmParams);

        if (enrollment.TemplateArguments != null)
        {
            var enrollmentArgs = enrollment.TemplateArguments
                .ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            TemplateArgumentsBuilder.Merge(templateArgs, enrollmentArgs);
        }

        var dto = mapper.Map<SequenceEnrollmentDetailsDto>(enrollment);
        dto.Steps = new List<EnrollmentStepTimelineEntryDto>();

        // Track the base time for calculating planned step schedules.
        // Start from the last known actual time (sent or scheduled delivery),
        // then chain forward using CalculateScheduledAt for planned steps.
        DateTime? lastKnownTime = null;

        foreach (var step in steps)
        {
            var entry = new EnrollmentStepTimelineEntryDto
            {
                StepId = step.Id,
                Name = step.Name,
                Position = step.Position,
                EmailTemplateId = step.EmailTemplateId,
                Timing = step.Timing,
            };

            if (deliveriesByStepId.TryGetValue(step.Id, out var delivery))
            {
                entry.DeliveryId = delivery.Id;
                entry.ScheduledAt = delivery.ScheduledAt;
                entry.EmailLogId = delivery.EmailLogId;

                switch (delivery.Status)
                {
                    case SequenceDeliveryStatus.Sent:
                        entry.Status = EnrollmentStepTimelineStatus.Sent;
                        entry.SentAt = delivery.SentAt;
                        lastKnownTime = delivery.SentAt ?? delivery.ScheduledAt;

                        if (delivery.EmailLogId.HasValue && emailLogsById.TryGetValue(delivery.EmailLogId.Value, out var emailLog))
                        {
                            entry.EmailPreview = new StepEmailPreviewDto
                            {
                                Subject = emailLog.Subject,
                                Body = ContactEmailCommunicationService.PrepareBody(emailLog),
                                FromEmail = emailLog.FromEmail,
                            };
                        }

                        break;
                    case SequenceDeliveryStatus.Scheduled:
                        entry.Status = EnrollmentStepTimelineStatus.Scheduled;
                        lastKnownTime = delivery.ScheduledAt;
                        break;
                    case SequenceDeliveryStatus.Failed:
                        entry.Status = EnrollmentStepTimelineStatus.Failed;
                        entry.ErrorMessage = delivery.ErrorMessage;
                        lastKnownTime = delivery.SentAt ?? delivery.ScheduledAt;
                        break;
                    case SequenceDeliveryStatus.Skipped:
                        entry.Status = EnrollmentStepTimelineStatus.Skipped;
                        entry.SkipReason = delivery.SkipReason;
                        lastKnownTime = delivery.ScheduledAt;
                        break;
                }
            }
            else
            {
                // No delivery exists — this is a planned step.
                entry.Status = EnrollmentStepTimelineStatus.Planned;

                var baseTime = lastKnownTime ?? enrollment.EnteredAt;
                var estimatedAt = CalculateScheduledAt(
                    baseTime,
                    step.Timing,
                    sequence.UseContactTimeZone,
                    sequence.TimeZone,
                    enrollment.Contact?.Timezone);

                entry.ScheduledAt = estimatedAt;
                lastKnownTime = estimatedAt;
            }

            // Render email preview from template for steps without an EmailLog
            if (entry.EmailPreview == null && templatesById.TryGetValue(step.EmailTemplateId, out var template))
            {
                entry.EmailPreview = new StepEmailPreviewDto
                {
                    Subject = await liquidTemplateService.RenderAsync(template.Subject, templateArgs),
                    Body = await liquidTemplateService.RenderAsync(template.BodyTemplate, templateArgs),
                    FromEmail = template.FromEmail,
                    FromName = template.FromName,
                };
            }

            dto.Steps.Add(entry);
        }

        return dto;
    }

    public async Task<bool> ExecuteDeliveryAsync(
        SequenceDelivery delivery,
        Sequence sequence,
        Contact contact,
        string templateName)
    {
        try
        {
            var templateArgs = TemplateArgumentsBuilder.FromContact(contact);

            var utmParams = UtmsBuilder.Create()
                .WithDefaults()
                .WithContext(new Utms { Campaign = sequence.Name })
                .WithOverrides(sequence.UtmParameters)
                .Build();

            TemplateArgumentsBuilder.WithUtmParameters(templateArgs, utmParams);

            // Merge enrollment-level custom template arguments
            var enrollment = await dbContext.SequenceEnrollments!
                .FirstOrDefaultAsync(e => e.Id == delivery.SequenceEnrollmentId);

            if (enrollment?.TemplateArguments != null)
            {
                var enrollmentArgs = enrollment.TemplateArguments
                    .ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                TemplateArgumentsBuilder.Merge(templateArgs, enrollmentArgs);
            }

            var emailLogId = await emailFromTemplateService.SendToContactAsync(
                delivery.ContactId,
                templateName,
                templateArgs,
                attachments: null);

            delivery.Status = SequenceDeliveryStatus.Sent;
            delivery.SentAt = DateTime.UtcNow;
            delivery.EmailLogId = emailLogId;

            if (enrollment != null)
            {
                enrollment.LastCompletedStepId = delivery.SequenceStepId;
            }

            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            delivery.Status = SequenceDeliveryStatus.Failed;
            delivery.ErrorMessage = $"Failed to send delivery for contact {delivery.ContactId}";
            await dbContext.SaveChangesAsync();
            return false;
        }
    }

    public async Task<(int sent, int failed, int skipped)> SendEligibleDeliveriesAsync(Sequence sequence)
    {
        var eligibleDeliveries = await dbContext.SequenceDeliveries!
            .Where(d => d.SequenceId == sequence.Id
                && d.Status == SequenceDeliveryStatus.Scheduled
                && d.ScheduledAt <= DateTime.UtcNow)
            .Include(d => d.SequenceStep)
            .OrderBy(d => d.ScheduledAt)
            .Take(100)
            .ToListAsync();

        if (eligibleDeliveries.Count == 0)
        {
            return (0, 0, 0);
        }

        var contactIds = eligibleDeliveries.Select(d => d.ContactId).Distinct().ToList();
        var contactsById = await TemplateContactLoader.LoadByIdsAsync(dbContext, contactIds);
        var unsubscribedIds = await GetUnsubscribedContactIdsAsync();

        HashSet<int>? repliedContactIds = null;
        if (sequence.StopOnReply)
        {
            repliedContactIds = await GetRepliedContactIdsAsync(sequence.Id, contactIds);
        }

        var stepTemplateIds = eligibleDeliveries
            .Where(d => d.SequenceStep != null)
            .Select(d => d.SequenceStep!.EmailTemplateId)
            .Distinct()
            .ToList();

        var templateNames = await dbContext.EmailTemplates!
            .Where(t => stepTemplateIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        int sent = 0, failed = 0, skipped = 0;

        foreach (var delivery in eligibleDeliveries)
        {
            contactsById.TryGetValue(delivery.ContactId, out var contact);

            if (unsubscribedIds.Contains(delivery.ContactId) || contact?.UnsubscribeId != null)
            {
                delivery.Status = SequenceDeliveryStatus.Skipped;
                delivery.SkipReason = "Unsubscribed";
                await ExitEnrollmentAsync(delivery.SequenceId, delivery.ContactId, SequenceExitReason.Unsubscribed);
                skipped++;
                await dbContext.SaveChangesAsync();
                continue;
            }

            if (repliedContactIds != null && repliedContactIds.Contains(delivery.ContactId))
            {
                delivery.Status = SequenceDeliveryStatus.Skipped;
                delivery.SkipReason = "ContactReplied";
                await ExitEnrollmentAsync(delivery.SequenceId, delivery.ContactId, SequenceExitReason.ReplyStopped);
                skipped++;
                await dbContext.SaveChangesAsync();
                continue;
            }

            if (contact == null || string.IsNullOrWhiteSpace(contact.Email))
            {
                delivery.Status = SequenceDeliveryStatus.Skipped;
                delivery.SkipReason = "InvalidEmail";
                skipped++;
                await dbContext.SaveChangesAsync();
                continue;
            }

            if (delivery.SequenceStep == null || !templateNames.TryGetValue(delivery.SequenceStep.EmailTemplateId, out var templateName))
            {
                delivery.Status = SequenceDeliveryStatus.Failed;
                delivery.ErrorMessage = "Email template not found";
                failed++;
                await dbContext.SaveChangesAsync();
                continue;
            }

            if (await ExecuteDeliveryAsync(delivery, sequence, contact, templateName))
            {
                sent++;
            }
            else
            {
                failed++;
            }
        }

        return (sent, failed, skipped);
    }

    public async Task<int> CompleteEnrollmentsAsync(Sequence sequence)
    {
        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequence.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        if (steps.Count == 0)
        {
            return 0;
        }

        var lastStepId = steps.Last().Id;

        var completableEnrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequence.Id
                && e.Status == SequenceEnrollmentStatus.Active
                && e.LastCompletedStepId == lastStepId)
            .ToListAsync();

        foreach (var enrollment in completableEnrollments)
        {
            enrollment.Status = SequenceEnrollmentStatus.Completed;
            enrollment.CompletedAt = DateTime.UtcNow;
            enrollment.ExitReason = SequenceExitReason.Completed;
        }

        if (completableEnrollments.Count > 0)
        {
            await dbContext.SaveChangesAsync();
        }

        return completableEnrollments.Count;
    }

    public async Task UpdateSequenceCountersAsync(Sequence sequence)
    {
        sequence.ActiveEnrollmentCount = await dbContext.SequenceEnrollments!
            .CountAsync(e => e.SequenceId == sequence.Id && e.Status == SequenceEnrollmentStatus.Active);

        sequence.CompletedEnrollmentCount = await dbContext.SequenceEnrollments!
            .CountAsync(e => e.SequenceId == sequence.Id && e.Status == SequenceEnrollmentStatus.Completed);

        sequence.ExitedEnrollmentCount = await dbContext.SequenceEnrollments!
            .CountAsync(e => e.SequenceId == sequence.Id && e.Status == SequenceEnrollmentStatus.Exited);

        sequence.SentCount = await dbContext.SequenceDeliveries!
            .CountAsync(d => d.SequenceId == sequence.Id && d.Status == SequenceDeliveryStatus.Sent);

        sequence.FailedCount = await dbContext.SequenceDeliveries!
            .CountAsync(d => d.SequenceId == sequence.Id && d.Status == SequenceDeliveryStatus.Failed);

        // Update step-level counters
        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequence.Id)
            .ToListAsync();

        var stepCounters = await dbContext.SequenceDeliveries!
            .Where(d => d.SequenceId == sequence.Id)
            .GroupBy(d => new { d.SequenceStepId, d.Status })
            .Select(g => new { g.Key.SequenceStepId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        foreach (var step in steps)
        {
            step.ScheduledCount = stepCounters
                .Where(c => c.SequenceStepId == step.Id && c.Status == SequenceDeliveryStatus.Scheduled)
                .Select(c => c.Count).FirstOrDefault();
            step.SentCount = stepCounters
                .Where(c => c.SequenceStepId == step.Id && c.Status == SequenceDeliveryStatus.Sent)
                .Select(c => c.Count).FirstOrDefault();
            step.FailedCount = stepCounters
                .Where(c => c.SequenceStepId == step.Id && c.Status == SequenceDeliveryStatus.Failed)
                .Select(c => c.Count).FirstOrDefault();
            step.SkippedCount = stepCounters
                .Where(c => c.SequenceStepId == step.Id && c.Status == SequenceDeliveryStatus.Skipped)
                .Select(c => c.Count).FirstOrDefault();
        }

        await dbContext.SaveChangesAsync();
    }

    private static DateTime AdvanceToAllowedWeekDay(DateTime localTime, string[] allowedWeekDays)
    {
        var allowedDays = allowedWeekDays
            .Select(d => Enum.TryParse<DayOfWeek>(d, ignoreCase: true, out var dow) ? dow : (DayOfWeek?)null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToHashSet();

        if (allowedDays.Count == 0)
        {
            return localTime;
        }

        for (int i = 0; i < 7; i++)
        {
            if (allowedDays.Contains(localTime.DayOfWeek))
            {
                return localTime;
            }

            localTime = localTime.AddDays(1);
        }

        return localTime;
    }

    private async Task ReconcileStepsAsync(Sequence sequence, List<SequenceStepCreateDto> incomingSteps)
    {
        var existingSteps = sequence.Steps?.ToList() ?? new List<SequenceStep>();
        var incomingIds = incomingSteps
            .Where(s => s.Id.HasValue)
            .Select(s => s.Id!.Value)
            .ToHashSet();

        // Remove steps whose IDs are not in the incoming list.
        var toRemove = existingSteps.Where(s => !incomingIds.Contains(s.Id)).ToList();

        // Reset enrollments pointing to deleted steps before removal
        if (toRemove.Count > 0)
        {
            var removedStepIds = toRemove.Select(s => s.Id).ToHashSet();
            var remainingStepIds = existingSteps
                .Where(s => incomingIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToHashSet();

            var affectedEnrollments = await dbContext.SequenceEnrollments!
                .Where(e => e.SequenceId == sequence.Id
                    && e.Status == SequenceEnrollmentStatus.Active
                    && e.LastCompletedStepId != null
                    && removedStepIds.Contains(e.LastCompletedStepId.Value))
                .ToListAsync();

            foreach (var enrollment in affectedEnrollments)
            {
                // Find the last sent delivery for a step that still exists
                var lastSentStepId = await dbContext.SequenceDeliveries!
                    .Where(d => d.SequenceEnrollmentId == enrollment.Id
                        && d.Status == SequenceDeliveryStatus.Sent
                        && remainingStepIds.Contains(d.SequenceStepId))
                    .OrderByDescending(d => d.SentAt)
                    .Select(d => (int?)d.SequenceStepId)
                    .FirstOrDefaultAsync();

                enrollment.LastCompletedStepId = lastSentStepId;
            }
        }

        dbContext.SequenceSteps!.RemoveRange(toRemove);

        // Update existing or add new steps with temporary negative positions.
        var existingById = existingSteps.ToDictionary(s => s.Id);

        for (int i = 0; i < incomingSteps.Count; i++)
        {
            var stepDto = incomingSteps[i];

            if (stepDto.Id.HasValue && existingById.TryGetValue(stepDto.Id.Value, out var existing))
            {
                existing.EmailTemplateId = stepDto.EmailTemplateId;
                existing.Name = stepDto.Name;
                existing.Timing = stepDto.Timing;
                existing.Type = stepDto.Type;
                existing.Position = -(i + 1);
            }
            else
            {
                var newStep = new SequenceStep
                {
                    SequenceId = sequence.Id,
                    Name = stepDto.Name,
                    EmailTemplateId = stepDto.EmailTemplateId,
                    Timing = stepDto.Timing,
                    Type = stepDto.Type,
                    Position = -(i + 1),
                };

                sequence.Steps ??= new List<SequenceStep>();
                sequence.Steps.Add(newStep);
            }
        }

        await dbContext.SaveChangesAsync();

        // Set final positions.
        var allSteps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequence.Id)
            .ToListAsync();

        var allStepsByName = allSteps.ToDictionary(s => s.Name);

        for (int i = 0; i < incomingSteps.Count; i++)
        {
            var step = allStepsByName[incomingSteps[i].Name];
            step.Position = i + 1;
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task ProcessImmediateStepsAsync(Sequence sequence, List<SequenceEnrollment> enrollments)
    {
        var activeEnrollments = enrollments
            .Where(e => e.Status == SequenceEnrollmentStatus.Active)
            .ToList();

        if (activeEnrollments.Count == 0)
        {
            return;
        }

        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequence.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        if (steps.Count == 0)
        {
            return;
        }

        var contactIds = activeEnrollments.Select(e => e.ContactId).Distinct().ToList();
        var contacts = await dbContext.Contacts!
            .Where(c => contactIds.Contains(c.Id))
            .ToListAsync();

        var contactsById = contacts.ToDictionary(c => c.Id);

        // Create Scheduled deliveries for all immediate steps (scheduledAt <= now)
        foreach (var enrollment in activeEnrollments)
        {
            if (!contactsById.TryGetValue(enrollment.ContactId, out var contact))
            {
                continue;
            }

            var baseTime = enrollment.EnteredAt;

            foreach (var step in steps)
            {
                var scheduledAt = CalculateScheduledAt(
                    baseTime,
                    step.Timing,
                    sequence.UseContactTimeZone,
                    sequence.TimeZone,
                    contact.Timezone);

                if (scheduledAt > DateTime.UtcNow)
                {
                    break;
                }

                dbContext.SequenceDeliveries!.Add(new SequenceDelivery
                {
                    SequenceId = sequence.Id,
                    SequenceEnrollmentId = enrollment.Id,
                    SequenceStepId = step.Id,
                    ContactId = enrollment.ContactId,
                    Status = SequenceDeliveryStatus.Scheduled,
                    ScheduledAt = scheduledAt,
                });
            }
        }

        await dbContext.SaveChangesAsync();

        // Send all eligible deliveries using the shared logic
        await SendEligibleDeliveriesAsync(sequence);

        // Complete enrollments where all steps have been delivered
        await CompleteEnrollmentsAsync(sequence);

        // Refresh counters
        await UpdateSequenceCountersAsync(sequence);
    }

    private async Task ExitEnrollmentAsync(int sequenceId, int contactId, SequenceExitReason reason)
    {
        var enrollment = await dbContext.SequenceEnrollments!
            .FirstOrDefaultAsync(e => e.SequenceId == sequenceId
                && e.ContactId == contactId
                && e.Status == SequenceEnrollmentStatus.Active);

        if (enrollment != null)
        {
            enrollment.Status = SequenceEnrollmentStatus.Exited;
            enrollment.ExitReason = reason;
            enrollment.ExitedAt = DateTime.UtcNow;
        }
    }

    private async Task<HashSet<int>> GetUnsubscribedContactIdsAsync()
    {
        var unsubscribedContactIds = await dbContext.Unsubscribes!
            .Where(u => u.ContactId != null)
            .Select(u => u.ContactId!.Value)
            .ToListAsync();
        return new HashSet<int>(unsubscribedContactIds);
    }

    private async Task<HashSet<int>> GetRepliedContactIdsAsync(int sequenceId, List<int> contactIds)
    {
        var enrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequenceId
                && contactIds.Contains(e.ContactId)
                && e.Status == SequenceEnrollmentStatus.Active)
            .Select(e => new { e.ContactId, e.EnteredAt })
            .ToListAsync();

        if (enrollments.Count == 0)
        {
            return new HashSet<int>();
        }

        var repliedIds = new HashSet<int>();
        foreach (var enrollment in enrollments)
        {
            var hasReply = await dbContext.EmailLogs!
                .AnyAsync(l => l.ContactId == enrollment.ContactId
                    && l.Status == EmailStatus.Received
                    && l.CreatedAt >= enrollment.EnteredAt);

            if (hasReply)
            {
                repliedIds.Add(enrollment.ContactId);
            }
        }

        return repliedIds;
    }
}
