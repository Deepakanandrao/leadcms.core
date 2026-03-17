// <copyright file="ISequenceService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Sequences.DTOs;
using LeadCMS.Entities;

namespace LeadCMS.Core.Sequences.Interfaces;

public interface ISequenceService
{
    Task<Sequence> ActivateAsync(int sequenceId);

    Task<Sequence> PauseAsync(int sequenceId);

    Task<Sequence> ArchiveAsync(int sequenceId);

    Task<SequenceStatisticsDto> GetStatisticsAsync(int sequenceId);

    Task<List<SequenceEnrollment>> EnrollContactsAsync(int sequenceId, int[] contactIds, string? enrollmentReason, SequenceEnrollmentSource source = SequenceEnrollmentSource.Api);

    Task<SequenceEnrollment> RemoveEnrollmentAsync(int sequenceId, int enrollmentId);
}
