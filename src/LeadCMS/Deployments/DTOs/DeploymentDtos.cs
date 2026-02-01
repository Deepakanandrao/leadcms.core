// <copyright file="DeploymentDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Deployments.Enums;

namespace LeadCMS.Core.Deployments.DTOs;

public class DeploymentTargetDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? Resource { get; set; }
}

public class DeploymentRecordDto
{
    public string Id { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string TargetName { get; set; } = string.Empty;

    public string? Resource { get; set; }

    public DeploymentStatus Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration { get; set; }

    public string? TriggeredById { get; set; }

    public string? TriggeredByName { get; set; }
}

public class DeploymentDetailsDto : DeploymentRecordDto
{
    public List<DeploymentStepDto>? Steps { get; set; }

    public List<string>? Logs { get; set; }

    public string? ErrorMessage { get; set; }
}

public class DeploymentStepDto
{
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DeploymentStatus Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets the URL to view this step in the provider's UI.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the URL to view logs for this step in the provider's UI.
    /// </summary>
    public string? LogsUrl { get; set; }
}

public class DeploymentStatsDto
{
    public int TotalDeployments { get; set; }

    public int SuccessfulDeployments { get; set; }

    public int FailedDeployments { get; set; }

    public int PendingDeployments { get; set; }

    public int InProgressDeployments { get; set; }

    public double SuccessRate { get; set; }

    public TimeSpan? AverageDuration { get; set; }
}

public class DeploymentTriggerResultDto
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public List<string> TriggeredDeploymentIds { get; set; } = new();

    public List<string>? Errors { get; set; }
}

public class DeploymentTriggerRequestDto
{
    /// <summary>
    /// Gets or sets optional list of target IDs to trigger. If null or empty and TriggerAll is false, validation error occurs.
    /// </summary>
    public List<string>? TargetIds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether if true, triggers all configured deployment targets. Takes precedence over TargetIds.
    /// </summary>
    public bool TriggerAll { get; set; }
}
