// <copyright file="SequenceSendTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using LeadCMS.Models;
using LeadCMS.Services;
using LeadCMS.Tasks;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.Sequences.Tasks;

/// <summary>
/// Background task that drives email sequence execution.
/// Each cycle: enrolls contacts from segments, schedules next deliveries,
/// sends eligible emails, and completes finished enrollments.
/// </summary>
public class SequenceSendTask : BaseTask
{
    private readonly PgDbContext dbContext;
    private readonly ISegmentService segmentService;
    private readonly IEmailFromTemplateService emailFromTemplateService;

    public SequenceSendTask(
        PgDbContext dbContext,
        ISegmentService segmentService,
        IEmailFromTemplateService emailFromTemplateService,
        IConfiguration configuration,
        TaskStatusService taskStatusService)
        : base("Tasks:SequenceSendTask", configuration, taskStatusService)
    {
        this.dbContext = dbContext;
        this.segmentService = segmentService;
        this.emailFromTemplateService = emailFromTemplateService;
    }

    public override async Task<bool> Execute(TaskExecutionLog currentJob)
    {
        try
        {
            int enrolled = 0;
            int scheduled = 0;
            int sent = 0;
            int failed = 0;
            int skipped = 0;
            int completed = 0;

            var activeSequences = await dbContext.Sequences!
                .Where(s => s.Status == SequenceStatus.Active)
                .ToListAsync();

            foreach (var sequence in activeSequences)
            {
                try
                {
                    enrolled += await ProcessSegmentEnrollments(sequence);
                    scheduled += await ScheduleNextDeliveries(sequence);
                    var (s, f, sk) = await SendEligibleDeliveries(sequence);
                    sent += s;
                    failed += f;
                    skipped += sk;
                    completed += await CompleteEnrollments(sequence);
                    await UpdateSequenceCounters(sequence);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error processing sequence Id={sequence.Id} Name={sequence.Name}");
                }
            }

            currentJob.Result = $"Sequences: {activeSequences.Count}, Enrolled: {enrolled}, Scheduled: {scheduled}, Sent: {sent}, Failed: {failed}, Skipped: {skipped}, Completed: {completed}";
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error occurred when executing SequenceSendTask in task runner {currentJob.Id}");
            currentJob.Result = $"Task execution failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Calculates the UTC time at which a delivery should be sent.
    /// </summary>
    /// <param name="baseTimeUtc">The base reference time in UTC.</param>
    /// <param name="timing">The step timing configuration.</param>
    /// <param name="useContactTimeZone">Whether to use the contact's timezone.</param>
    /// <param name="sequenceTimeZoneMinutes">The sequence-level timezone offset in minutes.</param>
    /// <param name="contactTimeZoneMinutes">The contact timezone offset in minutes, if available.</param>
    /// <returns>The calculated UTC send time.</returns>
    internal static DateTime CalculateScheduledAt(
        DateTime baseTimeUtc,
        SequenceStepTiming timing,
        bool useContactTimeZone,
        int sequenceTimeZoneMinutes,
        int? contactTimeZoneMinutes)
    {
        var offsetMinutes = useContactTimeZone
            ? contactTimeZoneMinutes ?? sequenceTimeZoneMinutes
            : sequenceTimeZoneMinutes;

        // Apply delay
        var delay = timing.Delay;
        var scheduledUtc = delay.Unit switch
        {
            "hours" => baseTimeUtc.AddHours(delay.Value),
            "days" => baseTimeUtc.AddDays(delay.Value),
            _ => baseTimeUtc.AddMinutes(delay.Value), // "minutes" or default
        };

        // If sendAt is specified, align to local time
        if (!string.IsNullOrEmpty(timing.SendAt) && TimeSpan.TryParse(timing.SendAt, out var sendAtTime))
        {
            // Convert scheduledUtc to local time
            var localTime = scheduledUtc.AddMinutes(offsetMinutes);
            var localDate = localTime.Date;
            var targetLocal = localDate.Add(sendAtTime);

            // If we've already passed the target time today, move to tomorrow
            if (localTime >= targetLocal)
            {
                targetLocal = targetLocal.AddDays(1);
            }

            // Apply allowed weekdays
            if (timing.AllowedWeekDays != null && timing.AllowedWeekDays.Length > 0)
            {
                targetLocal = AdvanceToAllowedWeekDay(targetLocal, timing.AllowedWeekDays);
            }

            // Convert back to UTC
            scheduledUtc = targetLocal.AddMinutes(-offsetMinutes);
        }
        else if (timing.AllowedWeekDays != null && timing.AllowedWeekDays.Length > 0)
        {
            // No sendAt but has weekday restrictions — align in local time
            var localTime = scheduledUtc.AddMinutes(offsetMinutes);
            localTime = AdvanceToAllowedWeekDay(localTime, timing.AllowedWeekDays);
            scheduledUtc = localTime.AddMinutes(-offsetMinutes);
        }

        return scheduledUtc;
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

        // Advance up to 7 days to find a matching day
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

    /// <summary>
    /// Step 1: For sequences with "segment" mode, enroll matching contacts.
    /// </summary>
    private async Task<int> ProcessSegmentEnrollments(Sequence sequence)
    {
        var enrollment = sequence.Enrollment;
        if (enrollment == null || !enrollment.Modes.Contains("segment"))
        {
            return 0;
        }

        if (enrollment.IncludeSegmentIds == null || enrollment.IncludeSegmentIds.Length == 0)
        {
            return 0;
        }

        // Resolve audience from segments
        var allContacts = new Dictionary<int, Contact>();
        foreach (var segmentId in enrollment.IncludeSegmentIds)
        {
            var contacts = await segmentService.GetSegmentContactsAsync(segmentId);
            foreach (var contact in contacts)
            {
                allContacts.TryAdd(contact.Id, contact);
            }
        }

        if (enrollment.ExcludeSegmentIds != null)
        {
            foreach (var segmentId in enrollment.ExcludeSegmentIds)
            {
                var contacts = await segmentService.GetSegmentContactsAsync(segmentId);
                foreach (var contact in contacts)
                {
                    allContacts.Remove(contact.Id);
                }
            }
        }

        // Check unsubscribed contacts
        var unsubscribedIds = await GetUnsubscribedContactIdsAsync();

        // Get existing enrollments for reentry policy check
        var existingEnrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequence.Id)
            .Select(e => new { e.ContactId, e.Status })
            .ToListAsync();

        var enrollmentsByContact = existingEnrollments
            .GroupBy(e => e.ContactId)
            .ToDictionary(g => g.Key, g => g.ToList());

        int enrolled = 0;
        foreach (var contact in allContacts.Values)
        {
            // Skip unsubscribed contacts
            if (unsubscribedIds.Contains(contact.Id) || contact.UnsubscribeId != null)
            {
                continue;
            }

            // Skip contacts without email
            if (string.IsNullOrWhiteSpace(contact.Email))
            {
                continue;
            }

            // Check reentry policy
            if (enrollmentsByContact.TryGetValue(contact.Id, out var existing))
            {
                var canEnroll = enrollment.ReentryPolicy switch
                {
                    ReentryPolicy.OnceEver => false,
                    ReentryPolicy.AllowAfterCompletion => existing.TrueForAll(e => e.Status == SequenceEnrollmentStatus.Completed),
                    ReentryPolicy.Always => !existing.Exists(e => e.Status == SequenceEnrollmentStatus.Active),
                    _ => false,
                };

                if (!canEnroll)
                {
                    continue;
                }
            }

            var newEnrollment = new SequenceEnrollment
            {
                SequenceId = sequence.Id,
                ContactId = contact.Id,
                Status = SequenceEnrollmentStatus.Active,
                EnteredAt = DateTime.UtcNow,
                EnrollmentSource = SequenceEnrollmentSource.Segment,
                EnrollmentReason = $"Matched segment enrollment criteria",
            };

            await dbContext.SequenceEnrollments!.AddAsync(newEnrollment);
            enrolled++;
        }

        if (enrolled > 0)
        {
            await dbContext.SaveChangesAsync();
        }

        return enrolled;
    }

    /// <summary>
    /// Step 2: For each active enrollment, schedule the next delivery if not already scheduled.
    /// </summary>
    private async Task<int> ScheduleNextDeliveries(Sequence sequence)
    {
        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequence.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        if (steps.Count == 0)
        {
            return 0;
        }

        var activeEnrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequence.Id && e.Status == SequenceEnrollmentStatus.Active)
            .Include(e => e.Contact)
            .ToListAsync();

        var enrollmentsToSchedule = activeEnrollments
            .GroupBy(e => e.ContactId)
            .Select(g => g
                .OrderByDescending(e => e.EnteredAt)
                .ThenByDescending(e => e.Id)
                .First())
            .ToList();

        int scheduled = 0;
        foreach (var enrollment in enrollmentsToSchedule)
        {
            // Find the next step
            SequenceStep? nextStep;
            DateTime baseTime;

            if (string.IsNullOrEmpty(enrollment.LastCompletedStepKey))
            {
                // First step
                nextStep = steps.FirstOrDefault();
                baseTime = enrollment.EnteredAt;
            }
            else
            {
                var lastCompletedIndex = steps.FindIndex(s => s.StepKey == enrollment.LastCompletedStepKey);
                if (lastCompletedIndex < 0 || lastCompletedIndex >= steps.Count - 1)
                {
                    continue; // All steps completed or step not found
                }

                nextStep = steps[lastCompletedIndex + 1];

                // Base time = SentAt of the last completed step's delivery
                var lastDelivery = await dbContext.SequenceDeliveries!
                    .Where(d => d.SequenceId == sequence.Id
                        && d.ContactId == enrollment.ContactId
                        && d.SequenceStepId == steps[lastCompletedIndex].Id
                        && d.Status == SequenceDeliveryStatus.Sent)
                    .Select(d => d.SentAt)
                    .FirstOrDefaultAsync();

                baseTime = lastDelivery ?? enrollment.EnteredAt;
            }

            if (nextStep == null)
            {
                continue;
            }

            // Calculate ScheduledAt
            var scheduledAt = CalculateScheduledAt(
                baseTime,
                nextStep.Timing,
                sequence.UseContactTimeZone,
                sequence.TimeZone,
                enrollment.Contact?.Timezone);

            if (await TryInsertScheduledDeliveryAsync(sequence.Id, nextStep.Id, enrollment.ContactId, scheduledAt))
            {
                scheduled++;
            }
        }

        return scheduled;
    }

    /// <summary>
    /// Step 3: Send deliveries that are due.
    /// </summary>
    private async Task<(int sent, int failed, int skipped)> SendEligibleDeliveries(Sequence sequence)
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

        // For StopOnReply, check if contacts have received emails (replies)
        HashSet<int>? repliedContactIds = null;
        if (sequence.StopOnReply)
        {
            repliedContactIds = await GetRepliedContactIdsAsync(sequence.Id, contactIds);
        }

        // Load template names for steps
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

            // Check unsubscribe
            if (unsubscribedIds.Contains(delivery.ContactId) || contact?.UnsubscribeId != null)
            {
                delivery.Status = SequenceDeliveryStatus.Skipped;
                delivery.SkipReason = "Unsubscribed";
                await ExitEnrollment(delivery.SequenceId, delivery.ContactId, SequenceExitReason.Unsubscribed);
                skipped++;
                await dbContext.SaveChangesAsync();
                continue;
            }

            // Check StopOnReply
            if (repliedContactIds != null && repliedContactIds.Contains(delivery.ContactId))
            {
                delivery.Status = SequenceDeliveryStatus.Skipped;
                delivery.SkipReason = "ContactReplied";
                await ExitEnrollment(delivery.SequenceId, delivery.ContactId, SequenceExitReason.ReplyStopped);
                skipped++;
                await dbContext.SaveChangesAsync();
                continue;
            }

            // Check contact has email
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

            try
            {
                var templateArgs = TemplateArgumentsBuilder.FromContact(contact);

                var utmParams = UtmsBuilder.Create()
                    .WithDefaults()
                    .WithContext(new Utms { Campaign = sequence.Name })
                    .WithOverrides(sequence.UtmParameters)
                    .Build();

                TemplateArgumentsBuilder.WithUtmParameters(templateArgs, utmParams);

                await emailFromTemplateService.SendToContactAsync(
                    delivery.ContactId,
                    templateName,
                    templateArgs,
                    attachments: null);

                delivery.Status = SequenceDeliveryStatus.Sent;
                delivery.SentAt = DateTime.UtcNow;

                // Try to link to the EmailLog that was just created
                var emailLog = await dbContext.EmailLogs!
                    .Where(l => l.ContactId == delivery.ContactId
                        && l.TemplateId == delivery.SequenceStep.EmailTemplateId
                        && l.Status == EmailStatus.Sent)
                    .OrderByDescending(l => l.CreatedAt)
                    .FirstOrDefaultAsync();

                if (emailLog != null)
                {
                    delivery.EmailLogId = emailLog.Id;
                }

                // Update enrollment's last completed step
                var enrollmentToUpdate = await dbContext.SequenceEnrollments!
                    .FirstOrDefaultAsync(e => e.SequenceId == delivery.SequenceId
                        && e.ContactId == delivery.ContactId
                        && e.Status == SequenceEnrollmentStatus.Active);

                if (enrollmentToUpdate != null)
                {
                    enrollmentToUpdate.LastCompletedStepKey = delivery.SequenceStep.StepKey;
                }

                sent++;
            }
            catch (Exception ex)
            {
                delivery.Status = SequenceDeliveryStatus.Failed;
                delivery.ErrorMessage = ex.Message;
                failed++;
                Log.Error(ex, $"Failed to send sequence delivery Id={delivery.Id} for contact {delivery.ContactId}");
            }

            await dbContext.SaveChangesAsync();
        }

        return (sent, failed, skipped);
    }

    /// <summary>
    /// Step 4: Mark enrollments as completed where all steps have been delivered.
    /// </summary>
    private async Task<int> CompleteEnrollments(Sequence sequence)
    {
        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequence.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        if (steps.Count == 0)
        {
            return 0;
        }

        var lastStepKey = steps.Last().StepKey;

        var completableEnrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequence.Id
                && e.Status == SequenceEnrollmentStatus.Active
                && e.LastCompletedStepKey == lastStepKey)
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

    /// <summary>
    /// Step 5: Refresh summary counters on the sequence entity from actual data.
    /// </summary>
    private async Task UpdateSequenceCounters(Sequence sequence)
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

        await dbContext.SaveChangesAsync();
    }

    private async Task<bool> TryInsertScheduledDeliveryAsync(
        int sequenceId,
        int sequenceStepId,
        int contactId,
        DateTime scheduledAt)
    {
        var rowsAffected = await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO sequence_delivery (sequence_id, sequence_step_id, contact_id, status, scheduled_at, created_at)
            VALUES ({sequenceId}, {sequenceStepId}, {contactId}, {(int)SequenceDeliveryStatus.Scheduled}, {scheduledAt}, {DateTime.UtcNow})
            ON CONFLICT (sequence_id, sequence_step_id, contact_id) DO NOTHING;");

        return rowsAffected > 0;
    }

    private async Task ExitEnrollment(int sequenceId, int contactId, SequenceExitReason reason)
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

    /// <summary>
    /// Checks for contacts who have replied to emails sent as part of this sequence.
    /// A reply is detected by looking for received emails (Status = Received) from the contact
    /// that arrived after their enrollment in the sequence.
    /// </summary>
    private async Task<HashSet<int>> GetRepliedContactIdsAsync(int sequenceId, List<int> contactIds)
    {
        // Get enrollment entry times for these contacts
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
