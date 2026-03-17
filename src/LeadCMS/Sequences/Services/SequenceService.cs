// <copyright file="SequenceService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Sequences.DTOs;
using LeadCMS.Core.Sequences.Interfaces;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using LeadCMS.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.Sequences.Services;

public class SequenceService : ISequenceService
{
    private readonly PgDbContext dbContext;
    private readonly IEmailFromTemplateService emailFromTemplateService;

    public SequenceService(PgDbContext dbContext, IEmailFromTemplateService emailFromTemplateService)
    {
        this.dbContext = dbContext;
        this.emailFromTemplateService = emailFromTemplateService;
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

        var enrollments = new List<SequenceEnrollment>();

        foreach (var contactId in contactIds)
        {
            _ = await dbContext.Contacts!.FindAsync(contactId)
                ?? throw new EntityNotFoundException(nameof(Contact), contactId.ToString());

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
                            if (existingEnrollment.Status != SequenceEnrollmentStatus.Completed)
                            {
                                throw new InvalidOperationException(
                                    $"Contact {contactId} is currently in this sequence and reentry policy is AllowAfterCompletion.");
                            }

                            break;
                        case ReentryPolicy.Always:
                            if (existingEnrollment.Status == SequenceEnrollmentStatus.Active)
                            {
                                throw new InvalidOperationException(
                                    $"Contact {contactId} already has an active enrollment in this sequence.");
                            }

                            break;
                    }
                }
            }

            var enrollment = new SequenceEnrollment
            {
                SequenceId = sequenceId,
                ContactId = contactId,
                Status = SequenceEnrollmentStatus.Active,
                EnteredAt = DateTime.UtcNow,
                EnrollmentSource = source,
                EnrollmentReason = enrollmentReason,
                TemplateArguments = templateArguments,
            };

            enrollments.Add(enrollment);
        }

        await dbContext.SequenceEnrollments!.AddRangeAsync(enrollments);
        sequence.ActiveEnrollmentCount += enrollments.Count;
        await dbContext.SaveChangesAsync();

        await ProcessImmediateStepsAsync(sequence, enrollments);

        return enrollments;
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

            await emailFromTemplateService.SendToContactAsync(
                delivery.ContactId,
                templateName,
                templateArgs,
                attachments: null);

            delivery.Status = SequenceDeliveryStatus.Sent;
            delivery.SentAt = DateTime.UtcNow;

            var emailLog = await dbContext.EmailLogs!
                .Where(l => l.ContactId == delivery.ContactId
                    && l.TemplateId == delivery.SequenceStep!.EmailTemplateId
                    && l.Status == EmailStatus.Sent)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            if (emailLog != null)
            {
                delivery.EmailLogId = emailLog.Id;
            }

            if (enrollment != null)
            {
                enrollment.LastCompletedStepName = delivery.SequenceStep!.Name;
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

        var lastStepName = steps.Last().Name;

        var completableEnrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequence.Id
                && e.Status == SequenceEnrollmentStatus.Active
                && e.LastCompletedStepName == lastStepName)
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
        var incomingNames = new HashSet<string>(incomingSteps.Select(s => s.Name));

        // Remove steps no longer in the incoming list.
        var toRemove = existingSteps.Where(s => !incomingNames.Contains(s.Name)).ToList();
        dbContext.SequenceSteps!.RemoveRange(toRemove);

        // Update existing or add new steps with temporary negative positions.
        for (int i = 0; i < incomingSteps.Count; i++)
        {
            var stepDto = incomingSteps[i];
            var existing = existingSteps.FirstOrDefault(s => s.Name == stepDto.Name);

            if (existing != null)
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

        for (int i = 0; i < incomingSteps.Count; i++)
        {
            var step = allSteps.First(s => s.Name == incomingSteps[i].Name);
            step.Position = i + 1;
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task ProcessImmediateStepsAsync(Sequence sequence, List<SequenceEnrollment> enrollments)
    {
        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequence.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        if (steps.Count == 0)
        {
            return;
        }

        var contactIds = enrollments.Select(e => e.ContactId).Distinct().ToList();
        var contacts = await dbContext.Contacts!
            .Where(c => contactIds.Contains(c.Id))
            .ToListAsync();

        var contactsById = contacts.ToDictionary(c => c.Id);

        // Create Scheduled deliveries for all immediate steps (scheduledAt <= now)
        foreach (var enrollment in enrollments)
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
