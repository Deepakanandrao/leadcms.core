// <copyright file="SequenceSendTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Sequences.Interfaces;
using LeadCMS.Core.Sequences.Services;
using LeadCMS.Data;
using LeadCMS.Entities;
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
    private readonly ISequenceService sequenceService;

    public SequenceSendTask(
        PgDbContext dbContext,
        ISegmentService segmentService,
        ISequenceService sequenceService,
        IConfiguration configuration,
        TaskStatusService taskStatusService)
        : base("Tasks:SequenceSendTask", configuration, taskStatusService)
    {
        this.dbContext = dbContext;
        this.segmentService = segmentService;
        this.sequenceService = sequenceService;
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
                    var (s, f, sk) = await sequenceService.SendEligibleDeliveriesAsync(sequence);
                    sent += s;
                    failed += f;
                    skipped += sk;
                    completed += await sequenceService.CompleteEnrollmentsAsync(sequence);
                    await sequenceService.UpdateSequenceCountersAsync(sequence);
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
        var excludedContactIds = new HashSet<int>();
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
                excludedContactIds.UnionWith(contacts.Select(contact => contact.Id));
                foreach (var contact in contacts)
                {
                    allContacts.Remove(contact.Id);
                }
            }
        }

        var exited = await ExitExcludedContactsAsync(sequence.Id, excludedContactIds);

        // Check unsubscribed contacts
        var unsubscribedIds = await GetUnsubscribedContactIdsAsync();

        // Segment-based enrollment is once per contact regardless of manual/API re-entry policy.
        var existingEnrollmentContactIds = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequence.Id)
            .Select(e => e.ContactId)
            .Distinct()
            .ToListAsync();
        var existingEnrollmentContactIdSet = existingEnrollmentContactIds.ToHashSet();

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

            if (existingEnrollmentContactIdSet.Contains(contact.Id))
            {
                continue;
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

        if (enrolled > 0 || exited > 0)
        {
            await dbContext.SaveChangesAsync();
        }

        return enrolled;
    }

    private async Task<int> ExitExcludedContactsAsync(int sequenceId, HashSet<int> excludedContactIds)
    {
        if (excludedContactIds.Count == 0)
        {
            return 0;
        }

        var activeEnrollments = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequenceId
                && e.Status == SequenceEnrollmentStatus.Active
                && excludedContactIds.Contains(e.ContactId))
            .ToListAsync();

        if (activeEnrollments.Count == 0)
        {
            return 0;
        }

        var exitedAt = DateTime.UtcNow;
        foreach (var enrollment in activeEnrollments)
        {
            enrollment.Status = SequenceEnrollmentStatus.Exited;
            enrollment.ExitReason = SequenceExitReason.ExcludedBySegment;
            enrollment.ExitedAt = exitedAt;
        }

        return activeEnrollments.Count;
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

            if (enrollment.LastCompletedStepId == null)
            {
                // First step
                nextStep = steps.FirstOrDefault();
                baseTime = enrollment.EnteredAt;
            }
            else
            {
                var lastCompletedIndex = steps.FindIndex(s => s.Id == enrollment.LastCompletedStepId);
                if (lastCompletedIndex < 0 || lastCompletedIndex >= steps.Count - 1)
                {
                    continue; // All steps completed or step not found
                }

                nextStep = steps[lastCompletedIndex + 1];

                // Base time = SentAt of the last completed step's delivery
                var lastDelivery = await dbContext.SequenceDeliveries!
                    .Where(d => d.SequenceEnrollmentId == enrollment.Id
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
            var scheduledAt = SequenceService.CalculateScheduledAt(
                baseTime,
                nextStep.Timing,
                sequence.UseContactTimeZone,
                sequence.TimeZone,
                enrollment.Contact?.Timezone);

            if (await TryInsertScheduledDeliveryAsync(sequence.Id, enrollment.Id, nextStep.Id, enrollment.ContactId, scheduledAt))
            {
                scheduled++;
            }
        }

        return scheduled;
    }

    private async Task<bool> TryInsertScheduledDeliveryAsync(
        int sequenceId,
        int sequenceEnrollmentId,
        int sequenceStepId,
        int contactId,
        DateTime scheduledAt)
    {
        var exists = await dbContext.SequenceDeliveries!
            .AnyAsync(d => d.SequenceEnrollmentId == sequenceEnrollmentId
                && d.SequenceStepId == sequenceStepId);

        if (exists)
        {
            return false;
        }

        dbContext.SequenceDeliveries!.Add(new SequenceDelivery
        {
            SequenceId = sequenceId,
            SequenceEnrollmentId = sequenceEnrollmentId,
            SequenceStepId = sequenceStepId,
            ContactId = contactId,
            Status = SequenceDeliveryStatus.Scheduled,
            ScheduledAt = scheduledAt,
        });

        await dbContext.SaveChangesAsync();
        return true;
    }

    private async Task<HashSet<int>> GetUnsubscribedContactIdsAsync()
    {
        var unsubscribedContactIds = await dbContext.Unsubscribes!
            .Where(u => u.ContactId != null)
            .Select(u => u.ContactId!.Value)
            .ToListAsync();
        return new HashSet<int>(unsubscribedContactIds);
    }
}
