// <copyright file="SequenceEnrollmentDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.DTOs;
using LeadCMS.Entities;

namespace LeadCMS.Core.Sequences.DTOs;

public class SequenceEnrollmentCreateDto
{
    [Required]
    public int[] ContactIds { get; set; } = Array.Empty<int>();

    public string? EnrollmentReason { get; set; }

    /// <summary>
    /// Gets or sets custom template arguments applied to every email sent in this enrollment.
    /// </summary>
    public Dictionary<string, string>? TemplateArguments { get; set; }
}

public class SequenceEnrollmentDetailsDto
{
    public int Id { get; set; }

    public int SequenceId { get; set; }

    public int ContactId { get; set; }

    public ContactDetailsDto? Contact { get; set; }

    public SequenceEnrollmentStatus Status { get; set; }

    public int? LastCompletedStepId { get; set; }

    public SequenceStepDetailsDto? LastCompletedStep { get; set; }

    public DateTime EnteredAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ExitedAt { get; set; }

    public SequenceExitReason ExitReason { get; set; }

    public SequenceEnrollmentSource EnrollmentSource { get; set; }

    public string? EnrollmentReason { get; set; }

    public Dictionary<string, string>? TemplateArguments { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
