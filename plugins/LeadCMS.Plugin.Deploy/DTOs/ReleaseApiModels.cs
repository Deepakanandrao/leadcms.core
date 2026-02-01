// <copyright file="ReleaseApiModels.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeadCMS.Plugin.Deploy.DTOs;

/// <summary>
/// Case-insensitive JSON string enum converter for Azure DevOps API responses.
/// </summary>
/// <typeparam name="T">The enum type.</typeparam>
public class CaseInsensitiveEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (Enum.TryParse<T>(stringValue, ignoreCase: true, out var result))
            {
                return result;
            }
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            var intValue = reader.GetInt32();
            return (T)Enum.ToObject(typeof(T), intValue);
        }

        return default;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Response wrapper for list of releases from Azure DevOps REST API.
/// </summary>
public class ReleaseListResponse
{
    /// <summary>
    /// Gets or sets the list of releases.
    /// </summary>
    public List<ReleaseReference>? Value { get; set; }
}

/// <summary>
/// Minimal release reference for list API responses.
/// </summary>
public class ReleaseReference
{
    /// <summary>
    /// Gets or sets the release ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the release name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Release details from Azure DevOps REST API.
/// Only includes fields needed for deployment tracking.
/// </summary>
public class ReleaseDetails
{
    /// <summary>
    /// Gets or sets the release ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the release name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the release status.
    /// </summary>
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<ReleaseApiStatus>))]
    public ReleaseApiStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the creation date.
    /// </summary>
    public DateTime CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the modification date.
    /// </summary>
    public DateTime ModifiedOn { get; set; }

    /// <summary>
    /// Gets or sets the release environments.
    /// </summary>
    public List<ReleaseEnvironment>? Environments { get; set; }

    /// <summary>
    /// Gets or sets the artifacts associated with this release.
    /// </summary>
    public List<ReleaseArtifact>? Artifacts { get; set; }
}

/// <summary>
/// Release status from Azure DevOps API.
/// </summary>
public enum ReleaseApiStatus
{
    /// <summary>
    /// Undefined status.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Draft release.
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Active release.
    /// </summary>
    Active = 2,

    /// <summary>
    /// Abandoned release.
    /// </summary>
    Abandoned = 4,
}

/// <summary>
/// Release environment/stage from Azure DevOps REST API.
/// </summary>
public class ReleaseEnvironment
{
    /// <summary>
    /// Gets or sets the environment ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment status.
    /// </summary>
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<EnvironmentApiStatus>))]
    public EnvironmentApiStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the deployment steps for this environment.
    /// </summary>
    public List<DeploymentAttempt>? DeploySteps { get; set; }
}

/// <summary>
/// Environment deployment status from Azure DevOps API.
/// </summary>
public enum EnvironmentApiStatus
{
    /// <summary>
    /// Undefined status.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Not started.
    /// </summary>
    NotStarted = 1,

    /// <summary>
    /// In progress.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Succeeded.
    /// </summary>
    Succeeded = 4,

    /// <summary>
    /// Canceled.
    /// </summary>
    Canceled = 8,

    /// <summary>
    /// Rejected.
    /// </summary>
    Rejected = 16,

    /// <summary>
    /// Queued.
    /// </summary>
    Queued = 32,

    /// <summary>
    /// Scheduled.
    /// </summary>
    Scheduled = 64,

    /// <summary>
    /// Partially succeeded.
    /// </summary>
    PartiallySucceeded = 128,
}

/// <summary>
/// Deployment attempt for an environment.
/// </summary>
public class DeploymentAttempt
{
    /// <summary>
    /// Gets or sets the attempt number.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Gets or sets the release deploy phases.
    /// </summary>
    public List<ReleaseDeployPhase>? ReleaseDeployPhases { get; set; }
}

/// <summary>
/// Release deploy phase.
/// </summary>
public class ReleaseDeployPhase
{
    /// <summary>
    /// Gets or sets the deployment jobs.
    /// </summary>
    public List<DeploymentJob>? DeploymentJobs { get; set; }
}

/// <summary>
/// Deployment job.
/// </summary>
public class DeploymentJob
{
    /// <summary>
    /// Gets or sets the tasks in this job.
    /// </summary>
    public List<ReleaseTask>? Tasks { get; set; }
}

/// <summary>
/// Release task.
/// </summary>
public class ReleaseTask
{
    /// <summary>
    /// Gets or sets the task ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the task name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the finish time.
    /// </summary>
    public DateTime? FinishTime { get; set; }

    /// <summary>
    /// Gets or sets the task status.
    /// </summary>
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<ReleaseTaskStatus>))]
    public ReleaseTaskStatus Status { get; set; }
}

/// <summary>
/// Task status.
/// </summary>
public enum ReleaseTaskStatus
{
    /// <summary>
    /// Unknown status.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Pending.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// In progress.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Success.
    /// </summary>
    Success = 3,

    /// <summary>
    /// Failure.
    /// </summary>
    Failure = 4,

    /// <summary>
    /// Canceled.
    /// </summary>
    Canceled = 5,

    /// <summary>
    /// Skipped.
    /// </summary>
    Skipped = 6,

    /// <summary>
    /// Succeeded with issues.
    /// </summary>
    SucceededWithIssues = 7,

    /// <summary>
    /// Failed.
    /// </summary>
    Failed = 8,

    /// <summary>
    /// Partially succeeded.
    /// </summary>
    PartiallySucceeded = 9,
}

/// <summary>
/// Release artifact.
/// </summary>
public class ReleaseArtifact
{
    /// <summary>
    /// Gets or sets the artifact type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the artifact alias.
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// Gets or sets the artifact definition reference.
    /// </summary>
    public Dictionary<string, ArtifactSourceReference>? DefinitionReference { get; set; }
}

/// <summary>
/// Artifact source reference.
/// </summary>
public class ArtifactSourceReference
{
    /// <summary>
    /// Gets or sets the ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Response from Azure DevOps REST API for project information.
/// </summary>
public class ProjectResponse
{
    /// <summary>
    /// Gets or sets the project ID (GUID).
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the project description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the project state (e.g., "wellFormed").
    /// </summary>
    public string? State { get; set; }
}
