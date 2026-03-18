// <copyright file="SequenceDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using LeadCMS.Models;

namespace LeadCMS.Core.Sequences.DTOs;

public class SequenceCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [LanguageCode]
    public string Language { get; set; } = string.Empty;

    public bool StopOnReply { get; set; }

    public bool UseContactTimeZone { get; set; }

    public int TimeZone { get; set; }

    public SequenceEnrollmentConfig? Enrollment { get; set; }

    public Utms? UtmParameters { get; set; }

    public List<SequenceStepCreateDto> Steps { get; set; } = new();
}

public class SequenceUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [MinLength(1)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [MinLength(1)]
    [LanguageCode(nullAllowed: true)]
    public string? Language { get; set; }

    public bool? StopOnReply { get; set; }

    public bool? UseContactTimeZone { get; set; }

    public int? TimeZone { get; set; }

    public SequenceEnrollmentConfig? Enrollment { get; set; }

    public Utms? UtmParameters { get; set; }

    public List<SequenceStepCreateDto>? Steps { get; set; }
}

public class SequenceDetailsDto : SequenceCreateDto
{
    public new List<SequenceStepDetailsDto> Steps { get; set; } = new();

    public int Id { get; set; }

    public SequenceStatus Status { get; set; }

    public DateTime? LastActivatedAt { get; set; }

    public DateTime? LastPausedAt { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public int ActiveEnrollmentCount { get; set; }

    public int CompletedEnrollmentCount { get; set; }

    public int ExitedEnrollmentCount { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class SequenceStatisticsDto
{
    public int ActiveEnrollmentCount { get; set; }

    public int CompletedEnrollmentCount { get; set; }

    public int ExitedEnrollmentCount { get; set; }

    public int SentCount { get; set; }

    public int FailedCount { get; set; }

    public int StepsCount { get; set; }
}


