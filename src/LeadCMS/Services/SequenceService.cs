// <copyright file="SequenceService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using LeadCMS.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

public class SequenceService : ISequenceService
{
    private readonly PgDbContext dbContext;

    public SequenceService(PgDbContext dbContext)
    {
        this.dbContext = dbContext;
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
            .Select(s => s.StepKey)
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

    public async Task<List<SequenceEnrollment>> EnrollContactsAsync(int sequenceId, int[] contactIds, string? enrollmentReason, SequenceEnrollmentSource source = SequenceEnrollmentSource.Api)
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
            };

            enrollments.Add(enrollment);
        }

        await dbContext.SequenceEnrollments!.AddRangeAsync(enrollments);
        sequence.ActiveEnrollmentCount += enrollments.Count;
        await dbContext.SaveChangesAsync();

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
}
