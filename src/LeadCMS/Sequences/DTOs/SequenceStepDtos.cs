// <copyright file="SequenceStepDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Models;

namespace LeadCMS.Core.Sequences.DTOs;

public class SequenceStepCreateDto
{
    /// <summary>
    /// Gets or sets the existing step ID. When set, the step is updated; when null, a new step is created.
    /// </summary>
    public int? Id { get; set; }

    [Required]
    public int EmailTemplateId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public SequenceStepType Type { get; set; } = SequenceStepType.Email;

    [Required]
    public SequenceStepTiming Timing { get; set; } = new();
}

public class SequenceStepDetailsDto
{
    public int Id { get; set; }

    public int SequenceId { get; set; }

    public int EmailTemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public SequenceStepType Type { get; set; }

    public SequenceStepTiming Timing { get; set; } = new();

    public int ScheduledCount { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    public int SkippedCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
