// <copyright file="ISequenceService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Sequences.DTOs;
using LeadCMS.Entities;
using LeadCMS.Models;

namespace LeadCMS.Core.Sequences.Interfaces;

public interface ISequenceService
{
    Task<Sequence> GetFullAsync(int sequenceId);

    Task<Sequence> SaveFullAsync(int? sequenceId, SequenceCreateDto dto);

    Task<Sequence> ReplaceStepsAsync(int sequenceId, List<SequenceStepCreateDto> steps);

    Task<Sequence> ActivateAsync(int sequenceId);

    Task<Sequence> PauseAsync(int sequenceId);

    Task<Sequence> ArchiveAsync(int sequenceId);

    Task<SequenceStatisticsDto> GetStatisticsAsync(int sequenceId);

    Task<List<SequenceEnrollment>> EnrollContactsAsync(int sequenceId, int[] contactIds, string? enrollmentReason, Dictionary<string, object>? templateArguments = null, SequenceEnrollmentSource source = SequenceEnrollmentSource.Api);

    Task<List<SequenceEnrollment>> EnrollContactBySequenceNameAsync(string sequenceName, int[] contactIds, string? enrollmentReason = null, Dictionary<string, object>? templateArguments = null, SequenceEnrollmentSource source = SequenceEnrollmentSource.Api);

    Task<SequenceEnrollment> RemoveEnrollmentAsync(int sequenceId, int enrollmentId);

    Task<List<SequenceEnrollment>> StopEnrollmentsAsync(int sequenceId, int[] enrollmentIds);

    /// <summary>
    /// Returns a single enrollment with its step timeline: executed, scheduled, and planned steps.
    /// </summary>
    /// <param name="sequenceId">The sequence ID.</param>
    /// <param name="enrollmentId">The enrollment ID.</param>
    /// <returns>The enrollment details with a step-by-step timeline.</returns>
    Task<SequenceEnrollmentDetailsDto> GetEnrollmentWithTimelineAsync(int sequenceId, int enrollmentId);

    /// <summary>
    /// Sends an email for a scheduled delivery: builds template args and UTMs,
    /// sends via email service, updates delivery status, links email log, and
    /// advances the enrollment's last completed step.
    /// </summary>
    /// <param name="delivery">The scheduled delivery to execute.</param>
    /// <param name="sequence">The parent sequence.</param>
    /// <param name="contact">The contact to send to.</param>
    /// <param name="templateName">The email template name.</param>
    /// <returns>True if sent successfully, false if failed.</returns>
    Task<bool> ExecuteDeliveryAsync(
        SequenceDelivery delivery,
        Sequence sequence,
        Contact contact,
        string templateName);

    /// <summary>
    /// Queries eligible scheduled deliveries for a sequence and sends them,
    /// handling unsubscribe/reply checks and enrollment exits.
    /// </summary>
    /// <param name="sequence">The sequence to process deliveries for.</param>
    /// <returns>Counts of sent, failed, and skipped deliveries.</returns>
    Task<(int sent, int failed, int skipped)> SendEligibleDeliveriesAsync(Sequence sequence);

    /// <summary>
    /// Marks enrollments as completed where all steps have been delivered.
    /// </summary>
    /// <param name="sequence">The sequence to check for completed enrollments.</param>
    /// <returns>The number of enrollments marked as completed.</returns>
    Task<int> CompleteEnrollmentsAsync(Sequence sequence);

    /// <summary>
    /// Refreshes summary counters on the sequence entity from actual data.
    /// </summary>
    /// <param name="sequence">The sequence to update counters for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSequenceCountersAsync(Sequence sequence);
}
