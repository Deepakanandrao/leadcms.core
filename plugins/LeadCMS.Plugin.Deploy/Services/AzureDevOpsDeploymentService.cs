// <copyright file="AzureDevOpsDeploymentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Deployments.DTOs;
using LeadCMS.Core.Deployments.Enums;
using LeadCMS.Core.Deployments.Exceptions;
using LeadCMS.Core.Deployments.Interfaces;
using LeadCMS.Plugin.Deploy.Configuration;
using LeadCMS.Plugin.Deploy.DTOs;

namespace LeadCMS.Plugin.Deploy.Services;

/// <summary>
/// Azure DevOps implementation of IDeploymentService.
/// Stateless - all data is fetched live from Azure DevOps APIs.
/// Thread-safe - can be used as a singleton.
/// </summary>
public class AzureDevOpsDeploymentService : IDeploymentService
{
    private readonly DeployPluginSettings settings;
    private readonly AzureDevOpsClient client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsDeploymentService"/> class.
    /// </summary>
    /// <param name="settings">The plugin settings.</param>
    /// <param name="client">The Azure DevOps client.</param>
    public AzureDevOpsDeploymentService(DeployPluginSettings settings, AzureDevOpsClient client)
    {
        this.settings = settings;
        this.client = client;
    }

    /// <inheritdoc/>
    public Task<List<DeploymentTargetDto>> GetTargetsAsync()
    {
        EnsureConfigured();
        var targets = settings.DeploymentTargets
            .Where(kvp => kvp.Value.BuildPipelineId > 0)
            .Select(kvp => new DeploymentTargetDto
            {
                Id = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Provider = "AzureDevOps",
                Resource = kvp.Value.Resource,
            }).ToList();

        return Task.FromResult(targets);
    }

    /// <inheritdoc/>
    public async Task<List<DeploymentRecordDto>> GetRecentDeploymentsAsync(int limit = 20)
    {
        EnsureConfigured();

        var definitionIds = settings.DeploymentTargets.Values
            .Select(t => t.BuildPipelineId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (!definitionIds.Any())
        {
            return new List<DeploymentRecordDto>();
        }

        var builds = await client.GetRecentBuildsAsync(definitionIds, limit);

        // Pre-fetch releases for all builds that need release tracking in one batch
        var buildsNeedingReleaseTracking = builds
            .Where(b => b.Status == BuildApiStatus.Completed && b.Result == BuildApiResult.Succeeded)
            .Where(b =>
            {
                var target = FindTargetForBuild(b);
                return !string.IsNullOrWhiteSpace(target?.settings.TrackReleaseStage);
            })
            .ToList();

        var releaseCache = buildsNeedingReleaseTracking.Any()
            ? await client.FindReleasesForBuildsAsync(buildsNeedingReleaseTracking)
            : new Dictionary<int, ReleaseDetails>();

        var records = new List<DeploymentRecordDto>();

        foreach (var build in builds)
        {
            var target = FindTargetForBuild(build);
            if (target == null)
            {
                continue;
            }

            var status = DetermineDeploymentStatus(build, target.Value.settings, releaseCache);

            // Get the release if already in cache for duration calculation
            ReleaseDetails? release = null;
            if (!string.IsNullOrWhiteSpace(target.Value.settings.TrackReleaseStage))
            {
                releaseCache.TryGetValue(build.Id, out release);
            }

            records.Add(new DeploymentRecordDto
            {
                Id = build.Id.ToString(),
                TargetId = target.Value.id,
                TargetName = target.Value.settings.Name,
                Resource = target.Value.settings.Resource,
                Status = status,
                StartedAt = build.StartTime ?? build.QueueTime ?? DateTime.UtcNow,
                CompletedAt = GetCompletionTime(build, target.Value.settings, release, status),
                Duration = CalculateDuration(build, target.Value.settings, release, status),
                TriggeredById = build.RequestedBy?.Id,
                TriggeredByName = build.RequestedBy?.DisplayName,
            });
        }

        return records.OrderByDescending(r => r.StartedAt).Take(limit).ToList();
    }

    /// <inheritdoc/>
    public async Task<DeploymentDetailsDto?> GetDeploymentAsync(string deploymentId)
    {
        EnsureConfigured();
        if (!int.TryParse(deploymentId, out int buildId))
        {
            return null;
        }

        var build = await client.GetBuildAsync(buildId);
        if (build == null)
        {
            return null;
        }

        var target = FindTargetForBuild(build);
        if (target == null)
        {
            return null;
        }

        // Fetch release once and reuse for both status and steps
        ReleaseDetails? release = null;
        if (!string.IsNullOrWhiteSpace(target.Value.settings.TrackReleaseStage) &&
            build.Status == BuildApiStatus.Completed &&
            build.Result == BuildApiResult.Succeeded)
        {
            release = await client.FindReleaseForBuildAsync(build);
        }

        var status = DetermineDeploymentStatusWithRelease(build, target.Value.settings, release);
        var steps = GetDeploymentSteps(build, target.Value.settings, release);

        return new DeploymentDetailsDto
        {
            Id = build.Id.ToString(),
            TargetId = target.Value.id,
            TargetName = target.Value.settings.Name,
            Resource = target.Value.settings.Resource,
            Status = status,
            StartedAt = build.StartTime ?? build.QueueTime ?? DateTime.UtcNow,
            CompletedAt = GetCompletionTime(build, target.Value.settings, release, status),
            Duration = CalculateDuration(build, target.Value.settings, release, status),
            TriggeredById = build.RequestedBy?.Id,
            TriggeredByName = build.RequestedBy?.DisplayName,
            Steps = steps,
            ErrorMessage = status == DeploymentStatus.Failed ? GetErrorMessage(build) : null,
        };
    }

    /// <inheritdoc/>
    public async Task<DeploymentStatsDto> GetStatsAsync()
    {
        EnsureConfigured();

        var definitionIds = settings.DeploymentTargets.Values
            .Select(t => t.BuildPipelineId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (!definitionIds.Any())
        {
            return new DeploymentStatsDto();
        }

        // Get recent builds to calculate stats
        var recentBuilds = await client.GetRecentBuildsAsync(definitionIds, 100);

        var completed = recentBuilds.Where(b => b.Status == BuildApiStatus.Completed).ToList();
        var successful = completed.Count(b => b.Result == BuildApiResult.Succeeded);
        var failed = completed.Count(b => b.Result == BuildApiResult.Failed);

        // Count in-progress
        var inProgress = recentBuilds.Count(b => b.Status == BuildApiStatus.InProgress);

        // Count pending/queued
        var pending = recentBuilds.Count(b => b.Status == BuildApiStatus.NotStarted);

        // Calculate average duration for successful builds
        var durations = completed
            .Where(b => b.Result == BuildApiResult.Succeeded && b.StartTime.HasValue && b.FinishTime.HasValue)
            .Select(b => b.FinishTime!.Value - b.StartTime!.Value)
            .ToList();

        TimeSpan? avgDuration = durations.Any()
            ? TimeSpan.FromTicks((long)durations.Average(d => d.Ticks))
            : null;

        return new DeploymentStatsDto
        {
            TotalDeployments = recentBuilds.Count,
            SuccessfulDeployments = successful,
            FailedDeployments = failed,
            PendingDeployments = pending,
            InProgressDeployments = inProgress,
            SuccessRate = completed.Any() ? (double)successful / completed.Count * 100 : 0,
            AverageDuration = avgDuration,
        };
    }

    /// <inheritdoc/>
    public async Task<DeploymentTriggerResultDto> TriggerAsync(string targetId, string? triggeredById)
    {
        return await TriggerAsync(new[] { targetId }, triggeredById);
    }

    /// <inheritdoc/>
    public async Task<DeploymentTriggerResultDto> TriggerAsync(IEnumerable<string> targetIds, string? triggeredById)
    {
        EnsureConfigured();

        var result = new DeploymentTriggerResultDto
        {
            Success = true,
            TriggeredDeploymentIds = new List<string>(),
            Errors = new List<string>(),
        };

        foreach (var targetId in targetIds)
        {
            if (!settings.DeploymentTargets.TryGetValue(targetId, out var target))
            {
                result.Errors.Add($"Target '{targetId}' not found.");
                continue;
            }

            if (target.BuildPipelineId <= 0)
            {
                result.Errors.Add($"Target '{targetId}' has no build pipeline configured.");
                continue;
            }

            var build = await client.TriggerBuildAsync(target.BuildPipelineId, target.SourceBranch);

            if (build != null)
            {
                result.TriggeredDeploymentIds.Add(build.Id.ToString());
            }
            else
            {
                result.Errors.Add($"Failed to trigger build for target '{targetId}'.");
            }
        }

        result.Success = result.TriggeredDeploymentIds.Any();
        result.Message = result.Success
            ? $"Successfully triggered {result.TriggeredDeploymentIds.Count} deployment(s)."
            : "No deployments were triggered.";

        return result;
    }

    /// <inheritdoc/>
    public async Task<DeploymentTriggerResultDto> TriggerAllAsync(string? triggeredById)
    {
        var allTargetIds = settings.DeploymentTargets
            .Where(kvp => kvp.Value.BuildPipelineId > 0)
            .Select(kvp => kvp.Key)
            .ToList();
        return await TriggerAsync(allTargetIds, triggeredById);
    }

    private (string id, DeploymentTargetSettings settings)? FindTargetForBuild(BuildDetails build)
    {
        var definitionId = build.Definition?.Id ?? 0;
        var sourceBranch = build.SourceBranch;

        foreach (var kvp in settings.DeploymentTargets)
        {
            if (kvp.Value.BuildPipelineId <= 0)
            {
                continue;
            }

            // Match on BuildPipelineId
            if (kvp.Value.BuildPipelineId != definitionId)
            {
                continue;
            }

            // If target has a SourceBranch configured, it must match
            if (!string.IsNullOrWhiteSpace(kvp.Value.SourceBranch))
            {
                if (string.Equals(kvp.Value.SourceBranch, sourceBranch, StringComparison.OrdinalIgnoreCase))
                {
                    return (kvp.Key, kvp.Value);
                }
            }
            else
            {
                // Target has no SourceBranch filter, matches any branch for this pipeline
                return (kvp.Key, kvp.Value);
            }
        }

        return null;
    }

    private void EnsureConfigured()
    {
        if (!settings.IsConfigured)
        {
            throw new DeploymentNotConfiguredException();
        }
    }

    /// <summary>
    /// Determines deployment status using a pre-fetched release cache.
    /// </summary>
    private DeploymentStatus DetermineDeploymentStatus(
        BuildDetails build,
        DeploymentTargetSettings target,
        Dictionary<int, ReleaseDetails> releaseCache)
    {
        releaseCache.TryGetValue(build.Id, out var release);
        return DetermineDeploymentStatusWithRelease(build, target, release);
    }

    /// <summary>
    /// Determines deployment status with an optional release.
    /// </summary>
    private DeploymentStatus DetermineDeploymentStatusWithRelease(
        BuildDetails build,
        DeploymentTargetSettings target,
        ReleaseDetails? release)
    {
        // If build is not complete, return based on build status
        if (build.Status != BuildApiStatus.Completed)
        {
            return build.Status switch
            {
                BuildApiStatus.NotStarted => DeploymentStatus.Pending,
                BuildApiStatus.InProgress => DeploymentStatus.InProgress,
                BuildApiStatus.Cancelling => DeploymentStatus.InProgress,
                _ => DeploymentStatus.Pending,
            };
        }

        // Build is complete - check result
        if (build.Result == BuildApiResult.Failed || build.Result == BuildApiResult.PartiallySucceeded)
        {
            return DeploymentStatus.Failed;
        }

        if (build.Result == BuildApiResult.Canceled)
        {
            return DeploymentStatus.Cancelled;
        }

        // Build succeeded - check if we need to track a release stage
        if (string.IsNullOrWhiteSpace(target.TrackReleaseStage))
        {
            return DeploymentStatus.Completed;
        }

        // Check release status for the specific stage
        if (release == null)
        {
            // Build succeeded but no release found yet - might still be triggering
            return DeploymentStatus.InProgress;
        }

        var (isComplete, success, _) = client.CheckReleaseCompletion(release, target.TrackReleaseStage);

        if (!isComplete)
        {
            return DeploymentStatus.InProgress;
        }

        return success ? DeploymentStatus.Completed : DeploymentStatus.Failed;
    }

    private DateTime? GetCompletionTime(BuildDetails build, DeploymentTargetSettings target, ReleaseDetails? release, DeploymentStatus status)
    {
        if (status != DeploymentStatus.Completed && status != DeploymentStatus.Failed && status != DeploymentStatus.Cancelled)
        {
            return null;
        }

        // If tracking release stage and release exists, use release completion time
        if (!string.IsNullOrWhiteSpace(target.TrackReleaseStage) && release != null)
        {
            var (_, releaseCompleted, isComplete) = client.GetReleaseDeploymentTiming(release, target.TrackReleaseStage);
            if (isComplete && releaseCompleted.HasValue)
            {
                return releaseCompleted;
            }
        }

        return build.FinishTime;
    }

    private TimeSpan? CalculateDuration(BuildDetails build, DeploymentTargetSettings target, ReleaseDetails? release, DeploymentStatus status)
    {
        if (!build.StartTime.HasValue)
        {
            return null;
        }

        var startTime = build.StartTime.Value;

        // For completed/failed deployments with release tracking
        if ((status == DeploymentStatus.Completed || status == DeploymentStatus.Failed) &&
            !string.IsNullOrWhiteSpace(target.TrackReleaseStage) &&
            release != null)
        {
            var (_, releaseCompleted, isComplete) = client.GetReleaseDeploymentTiming(release, target.TrackReleaseStage);
            if (isComplete && releaseCompleted.HasValue)
            {
                return releaseCompleted.Value - startTime;
            }
        }

        // For completed/failed without release tracking, use build finish time
        if ((status == DeploymentStatus.Completed || status == DeploymentStatus.Failed || status == DeploymentStatus.Cancelled) &&
            build.FinishTime.HasValue)
        {
            return build.FinishTime.Value - startTime;
        }

        // For in-progress, show current duration
        if (status == DeploymentStatus.InProgress)
        {
            return DateTime.UtcNow - startTime;
        }

        return null;
    }

    /// <summary>
    /// Gets deployment steps with a pre-fetched release (avoids duplicate API call).
    /// </summary>
    private List<DeploymentStepDto> GetDeploymentSteps(
        BuildDetails build,
        DeploymentTargetSettings target,
        ReleaseDetails? release)
    {
        var steps = new List<DeploymentStepDto>();

        // Add build as first step
        steps.Add(new DeploymentStepDto
        {
            Id = build.Id.ToString(),
            Name = "Build",
            Status = MapBuildStatusToDeploymentStatus(build),
            StartedAt = build.StartTime,
            CompletedAt = build.FinishTime,
            Duration = build.StartTime.HasValue && build.FinishTime.HasValue
                ? build.FinishTime.Value - build.StartTime.Value
                : null,
            Url = client.GetBuildUrl(build),
            LogsUrl = client.GetBuildLogsUrl(build),
        });

        // If tracking release stage, always add the release step
        if (!string.IsNullOrWhiteSpace(target.TrackReleaseStage))
        {
            var stageName = target.TrackReleaseStage;

            // Build not completed yet - release step is pending
            if (build.Status != BuildApiStatus.Completed)
            {
                steps.Add(new DeploymentStepDto
                {
                    Name = $"Release ({stageName})",
                    Status = DeploymentStatus.Pending,
                });
            }
            else if (build.Result != BuildApiResult.Succeeded)
            {
                // Build failed or canceled - release step won't run
                steps.Add(new DeploymentStepDto
                {
                    Name = $"Release ({stageName})",
                    Status = DeploymentStatus.Cancelled,
                });
            }
            else if (release != null)
            {
                var (isComplete, success, _) = client.CheckReleaseCompletion(release, stageName);
                var (releaseStarted, releaseCompleted, _) = client.GetReleaseDeploymentTiming(release, stageName);

                var releaseStatus = DeploymentStatus.InProgress;
                if (isComplete)
                {
                    releaseStatus = success ? DeploymentStatus.Completed : DeploymentStatus.Failed;
                }

                TimeSpan? releaseDuration = null;
                if (releaseStarted.HasValue)
                {
                    releaseDuration = releaseCompleted.HasValue
                        ? releaseCompleted.Value - releaseStarted.Value
                        : DateTime.UtcNow - releaseStarted.Value;
                }

                var environmentId = client.GetEnvironmentId(release, stageName);

                steps.Add(new DeploymentStepDto
                {
                    Id = release.Id.ToString(),
                    Name = $"Release ({stageName})",
                    Status = releaseStatus,
                    StartedAt = releaseStarted,
                    CompletedAt = releaseCompleted,
                    Duration = releaseDuration,
                    Url = client.GetReleaseUrl(release.Id),
                    LogsUrl = client.GetReleaseLogsUrl(release.Id, environmentId),
                });
            }
            else
            {
                // Build succeeded but release not found yet - pending
                steps.Add(new DeploymentStepDto
                {
                    Name = $"Release ({stageName})",
                    Status = DeploymentStatus.Pending,
                });
            }
        }

        return steps;
    }

    private DeploymentStatus MapBuildStatusToDeploymentStatus(BuildDetails build)
    {
        if (build.Status != BuildApiStatus.Completed)
        {
            return build.Status switch
            {
                BuildApiStatus.NotStarted => DeploymentStatus.Pending,
                BuildApiStatus.InProgress => DeploymentStatus.InProgress,
                _ => DeploymentStatus.InProgress,
            };
        }

        return build.Result switch
        {
            BuildApiResult.Succeeded => DeploymentStatus.Completed,
            BuildApiResult.PartiallySucceeded => DeploymentStatus.Completed,
            BuildApiResult.Failed => DeploymentStatus.Failed,
            BuildApiResult.Canceled => DeploymentStatus.Cancelled,
            _ => DeploymentStatus.Failed,
        };
    }

    private string? GetErrorMessage(BuildDetails build)
    {
        if (build.Result == BuildApiResult.Failed)
        {
            return $"Build failed. See Azure DevOps for details: {client.OrganizationUrl}/{client.ProjectName}/_build/results?buildId={build.Id}";
        }

        return null;
    }
}
