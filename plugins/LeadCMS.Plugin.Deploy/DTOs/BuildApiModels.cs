// <copyright file="BuildApiModels.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json.Serialization;

namespace LeadCMS.Plugin.Deploy.DTOs;

/// <summary>
/// Response wrapper for list of builds from Azure DevOps REST API.
/// </summary>
public class BuildListResponse
{
    /// <summary>
    /// Gets or sets the list of builds.
    /// </summary>
    public List<BuildDetails>? Value { get; set; }
}

/// <summary>
/// Build details from Azure DevOps REST API.
/// </summary>
public class BuildDetails
{
    /// <summary>
    /// Gets or sets the build ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the build number.
    /// </summary>
    public string? BuildNumber { get; set; }

    /// <summary>
    /// Gets or sets the build status.
    /// </summary>
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<BuildApiStatus>))]
    public BuildApiStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the build result.
    /// </summary>
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<BuildApiResult>))]
    public BuildApiResult? Result { get; set; }

    /// <summary>
    /// Gets or sets the queue time.
    /// </summary>
    public DateTime? QueueTime { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the finish time.
    /// </summary>
    public DateTime? FinishTime { get; set; }

    /// <summary>
    /// Gets or sets the source branch.
    /// </summary>
    public string? SourceBranch { get; set; }

    /// <summary>
    /// Gets or sets the source version (commit SHA).
    /// </summary>
    public string? SourceVersion { get; set; }

    /// <summary>
    /// Gets or sets the build definition.
    /// </summary>
    public BuildDefinitionReference? Definition { get; set; }

    /// <summary>
    /// Gets or sets the project reference.
    /// </summary>
    public ProjectReference? Project { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the build.
    /// </summary>
    public IdentityReference? RequestedBy { get; set; }

    /// <summary>
    /// Gets or sets the user who requested the build for.
    /// </summary>
    public IdentityReference? RequestedFor { get; set; }

    /// <summary>
    /// Gets or sets the build links.
    /// </summary>
    [JsonPropertyName("_links")]
    public BuildLinks? Links { get; set; }

    /// <summary>
    /// Gets or sets the build URL.
    /// </summary>
    public string? Url { get; set; }
}

/// <summary>
/// Build definition reference.
/// </summary>
public class BuildDefinitionReference
{
    /// <summary>
    /// Gets or sets the definition ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the definition name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the definition path.
    /// </summary>
    public string? Path { get; set; }
}

/// <summary>
/// Full build definition details.
/// </summary>
public class BuildDefinitionDetails
{
    /// <summary>
    /// Gets or sets the definition ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the definition name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the definition path.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the repository information.
    /// </summary>
    public RepositoryReference? Repository { get; set; }

    /// <summary>
    /// Gets or sets the project reference.
    /// </summary>
    public ProjectReference? Project { get; set; }
}

/// <summary>
/// Repository reference.
/// </summary>
public class RepositoryReference
{
    /// <summary>
    /// Gets or sets the repository ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the repository type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the repository name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the default branch.
    /// </summary>
    public string? DefaultBranch { get; set; }
}

/// <summary>
/// Project reference.
/// </summary>
public class ProjectReference
{
    /// <summary>
    /// Gets or sets the project ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the project name.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// Identity reference for users.
/// </summary>
public class IdentityReference
{
    /// <summary>
    /// Gets or sets the identity ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the unique name (email or domain\username).
    /// </summary>
    public string? UniqueName { get; set; }
}

/// <summary>
/// Build links.
/// </summary>
public class BuildLinks
{
    /// <summary>
    /// Gets or sets the web link.
    /// </summary>
    public LinkReference? Web { get; set; }

    /// <summary>
    /// Gets or sets the timeline link.
    /// </summary>
    public LinkReference? Timeline { get; set; }

    /// <summary>
    /// Gets or sets the badge link.
    /// </summary>
    public LinkReference? Badge { get; set; }
}

/// <summary>
/// Link reference.
/// </summary>
public class LinkReference
{
    /// <summary>
    /// Gets or sets the href.
    /// </summary>
    public string? Href { get; set; }
}

/// <summary>
/// Build status enum matching Azure DevOps API.
/// </summary>
public enum BuildApiStatus
{
    /// <summary>No status.</summary>
    None = 0,

    /// <summary>Build is in progress.</summary>
    InProgress = 1,

    /// <summary>Build has completed.</summary>
    Completed = 2,

    /// <summary>Build is cancelling.</summary>
    Cancelling = 4,

    /// <summary>Build is postponed.</summary>
    Postponed = 8,

    /// <summary>Build is not started.</summary>
    NotStarted = 32,

    /// <summary>All statuses.</summary>
    All = 47,
}

/// <summary>
/// Build result enum matching Azure DevOps API.
/// </summary>
public enum BuildApiResult
{
    /// <summary>No result.</summary>
    None = 0,

    /// <summary>Build succeeded.</summary>
    Succeeded = 2,

    /// <summary>Build partially succeeded.</summary>
    PartiallySucceeded = 4,

    /// <summary>Build failed.</summary>
    Failed = 8,

    /// <summary>Build was canceled.</summary>
    Canceled = 32,
}

/// <summary>
/// Build queue request.
/// </summary>
public class BuildQueueRequest
{
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public BuildDefinitionReference? Definition { get; set; }

    /// <summary>
    /// Gets or sets the source branch.
    /// </summary>
    public string? SourceBranch { get; set; }

    /// <summary>
    /// Gets or sets the project.
    /// </summary>
    public ProjectReference? Project { get; set; }
}

/// <summary>
/// Build timeline from Azure DevOps REST API.
/// </summary>
public class BuildTimeline
{
    /// <summary>
    /// Gets or sets the timeline ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the change ID.
    /// </summary>
    public int ChangeId { get; set; }

    /// <summary>
    /// Gets or sets the timeline records.
    /// </summary>
    public List<TimelineRecord>? Records { get; set; }
}

/// <summary>
/// Timeline record representing a step in the build.
/// </summary>
public class TimelineRecord
{
    /// <summary>
    /// Gets or sets the record ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the parent record ID.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the record type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the record name.
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
    /// Gets or sets the state.
    /// </summary>
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<TimelineRecordState>))]
    public TimelineRecordState? State { get; set; }

    /// <summary>
    /// Gets or sets the result.
    /// </summary>
    [JsonConverter(typeof(CaseInsensitiveEnumConverter<TaskResultApi>))]
    public TaskResultApi? Result { get; set; }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public int? Order { get; set; }

    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    public int? ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the warning count.
    /// </summary>
    public int? WarningCount { get; set; }
}

/// <summary>
/// Timeline record state.
/// </summary>
public enum TimelineRecordState
{
    /// <summary>Pending state.</summary>
    Pending = 0,

    /// <summary>In progress state.</summary>
    InProgress = 1,

    /// <summary>Completed state.</summary>
    Completed = 2,
}

/// <summary>
/// Task result enum for timeline records.
/// </summary>
public enum TaskResultApi
{
    /// <summary>Succeeded.</summary>
    Succeeded = 0,

    /// <summary>Succeeded with issues.</summary>
    SucceededWithIssues = 1,

    /// <summary>Failed.</summary>
    Failed = 2,

    /// <summary>Canceled.</summary>
    Canceled = 3,

    /// <summary>Skipped.</summary>
    Skipped = 4,

    /// <summary>Abandoned.</summary>
    Abandoned = 5,
}
