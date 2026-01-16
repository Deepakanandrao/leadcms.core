// <copyright file="TaskDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.DTOs;

public class TaskDetailsDto
{
    public string Name { get; set; } = string.Empty;

    public string CronSchedule { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public int RetryInterval { get; set; }

    public bool IsRunning { get; set; }
}

public class TaskExecutionDto
{
    public string Name { get; set; } = string.Empty;

    public bool Completed { get; set; } = false;
}

public class TaskExecutionLogDetailsDto
{
    public int Id { get; set; }

    public string TaskName { get; set; } = string.Empty;

    public DateTime ScheduledExecutionTime { get; set; }

    public DateTime ActualExecutionTime { get; set; }

    public string Status { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public string? Result { get; set; }

    public string? Source { get; set; }

    public string TriggeredBy { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }
}