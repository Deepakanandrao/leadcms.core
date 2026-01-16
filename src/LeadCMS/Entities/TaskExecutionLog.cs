// <copyright file="TaskExecutionLog.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeadCMS.Entities;

public enum TaskExecutionStatus
{
    Pending = 0,
    Completed = 1,
}

public enum TaskExecutionTrigger
{
    Scheduled = 0,
    Manual = 1,
}

[Table("task_execution_log")]
public class TaskExecutionLog : BaseEntityWithId
{
    [Required]
    public string TaskName { get; set; } = string.Empty;

    public DateTime ScheduledExecutionTime { get; set; }

    public DateTime ActualExecutionTime { get; set; }

    public TaskExecutionStatus Status { get; set; }

    public int RetryCount { get; set; } = 0;

    public string? Result { get; set; }

    public TaskExecutionTrigger TriggeredBy { get; set; } = TaskExecutionTrigger.Scheduled;

    public TimeSpan Duration { get; set; }
}