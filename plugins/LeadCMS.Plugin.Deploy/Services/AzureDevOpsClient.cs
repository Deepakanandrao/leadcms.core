// <copyright file="AzureDevOpsClient.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeadCMS.Plugin.Deploy.Configuration;
using LeadCMS.Plugin.Deploy.DTOs;
using Microsoft.Extensions.Logging;

namespace LeadCMS.Plugin.Deploy.Services;

/// <summary>
/// Client for Azure DevOps API operations.
/// Thread-safe - can be used as a singleton across multiple requests.
/// Uses HttpClient for all API calls to avoid SDK deserialization issues.
/// </summary>
public class AzureDevOpsClient : IDisposable
{
    private readonly AzureDevOpsSettings settings;
    private readonly ILogger<AzureDevOpsClient>? logger;
    private readonly SemaphoreSlim projectIdLock = new(1, 1);
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureDevOpsClient"/> class.
    /// </summary>
    /// <param name="settings">The Azure DevOps settings.</param>
    /// <param name="logger">Optional logger instance.</param>
    public AzureDevOpsClient(AzureDevOpsSettings settings, ILogger<AzureDevOpsClient>? logger = null)
    {
        this.settings = settings;
        this.logger = logger;

        // Initialize HttpClient for all API calls
        httpClient = new HttpClient();
        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{settings.PersonalAccessToken}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>
    /// Gets the organization URL.
    /// </summary>
    public string OrganizationUrl => settings.OrganizationUrl;

    /// <summary>
    /// Gets the project name.
    /// </summary>
    public string ProjectName => settings.ProjectName;

    /// <summary>
    /// Tests the connection to Azure DevOps and resolves the project ID.
    /// </summary>
    /// <returns>True if connection is successful.</returns>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Use REST API directly to get project info
            var projectUrl = $"{settings.OrganizationUrl}/_apis/projects/{Uri.EscapeDataString(settings.ProjectName)}?api-version=7.1";
            var project = await GetFromApiAsync<ProjectResponse>(projectUrl);

            if (project == null || string.IsNullOrEmpty(project.Id))
            {
                throw new InvalidOperationException($"Project '{settings.ProjectName}' not found in organization");
            }

            settings.ProjectId = project.Id;
            return true;
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError(ex, "Failed to connect to Azure DevOps organization {OrganizationUrl} project {ProjectName}", settings.OrganizationUrl, settings.ProjectName);
            throw new InvalidOperationException($"Failed to connect to Azure DevOps: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to test connection to Azure DevOps organization {OrganizationUrl} project {ProjectName}", settings.OrganizationUrl, settings.ProjectName);
            throw new InvalidOperationException($"Failed to connect to Azure DevOps: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Triggers a build pipeline.
    /// </summary>
    /// <param name="definitionId">The build definition ID.</param>
    /// <param name="sourceBranch">Optional source branch.</param>
    /// <returns>The queued build, or null if failed.</returns>
    public async Task<BuildDetails?> TriggerBuildAsync(int definitionId, string? sourceBranch = null)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            // Get build definition to determine default branch if not specified
            string branch = sourceBranch ?? "refs/heads/main";
            if (string.IsNullOrWhiteSpace(sourceBranch))
            {
                var definition = await GetBuildDefinitionAsync(definitionId);
                if (definition?.Repository?.DefaultBranch != null)
                {
                    branch = definition.Repository.DefaultBranch;
                }
            }

            var buildRequest = new BuildQueueRequest
            {
                Definition = new BuildDefinitionReference { Id = definitionId },
                SourceBranch = branch,
                Project = new ProjectReference
                {
                    Id = settings.ProjectId!,
                    Name = settings.ProjectName,
                },
            };

            var url = $"{GetBuildBaseUrl()}/_apis/build/builds?api-version=7.1";
            return await PostToApiAsync<BuildDetails>(url, buildRequest);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to trigger build for definition {DefinitionId} on branch {SourceBranch}", definitionId, sourceBranch);
            throw new InvalidOperationException($"Failed to trigger build for definition {definitionId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the status of a build.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <returns>The build, or null if not found.</returns>
    public async Task<BuildDetails?> GetBuildAsync(int buildId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            var url = $"{GetBuildBaseUrl()}/_apis/build/builds/{buildId}?api-version=7.1";
            return await GetFromApiAsync<BuildDetails>(url);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to get build {BuildId}", buildId);
            throw new InvalidOperationException($"Failed to get build {buildId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the build definition.
    /// </summary>
    /// <param name="definitionId">The build definition ID.</param>
    /// <returns>The build definition, or null if not found.</returns>
    public async Task<BuildDefinitionDetails?> GetBuildDefinitionAsync(int definitionId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            var url = $"{GetBuildBaseUrl()}/_apis/build/definitions/{definitionId}?api-version=7.1";
            return await GetFromApiAsync<BuildDefinitionDetails>(url);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to get build definition {DefinitionId}", definitionId);
            throw new InvalidOperationException($"Failed to get build definition {definitionId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets recent builds for the specified pipeline definitions.
    /// </summary>
    /// <param name="definitionIds">The build definition IDs.</param>
    /// <param name="top">Maximum number of builds to return.</param>
    /// <returns>A list of builds.</returns>
    public async Task<List<BuildDetails>> GetRecentBuildsAsync(IEnumerable<int> definitionIds, int top = 20)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            var definitions = string.Join(",", definitionIds);
            var url = $"{GetBuildBaseUrl()}/_apis/build/builds?" +
                $"definitions={definitions}" +
                $"&queryOrder=startTimeDescending" +
                $"&$top={top}" +
                $"&api-version=7.1";

            var response = await GetFromApiAsync<BuildListResponse>(url);
            return response?.Value ?? new List<BuildDetails>();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to get recent builds for definitions {DefinitionIds}", string.Join(", ", definitionIds));
            throw new InvalidOperationException($"Failed to get recent builds: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets builds with specific statuses.
    /// </summary>
    /// <param name="definitionIds">The build definition IDs.</param>
    /// <param name="status">The build status to filter.</param>
    /// <param name="top">Maximum number of builds to return.</param>
    /// <returns>A list of builds.</returns>
    public async Task<List<BuildDetails>> GetBuildsWithStatusAsync(IEnumerable<int> definitionIds, BuildApiStatus status, int top = 50)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            var definitions = string.Join(",", definitionIds);
            var url = $"{GetBuildBaseUrl()}/_apis/build/builds?" +
                $"definitions={definitions}" +
                $"&statusFilter={status}" +
                $"&queryOrder=startTimeDescending" +
                $"&$top={top}" +
                $"&api-version=7.1";

            var response = await GetFromApiAsync<BuildListResponse>(url);
            return response?.Value ?? new List<BuildDetails>();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to get builds with status {Status} for definitions {DefinitionIds}", status, string.Join(", ", definitionIds));
            throw new InvalidOperationException($"Failed to get builds with status {status}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Finds a release triggered by a specific build using the artifact source ID filter.
    /// This is more reliable than time-based filtering and works for historical builds.
    /// </summary>
    /// <param name="build">The build to find releases for.</param>
    /// <returns>The release, or null if not found.</returns>
    public async Task<ReleaseDetails?> FindReleaseForBuildAsync(BuildDetails build)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            // Use sourceId filter to find releases that used this build as an artifact.
            // The sourceId format is: "{projectId}:{buildDefinitionId}"
            var sourceId = $"{settings.ProjectId}:{build.Definition?.Id}";

            var releaseListUrl = $"{GetVsrmBaseUrl()}/_apis/release/releases?" +
                $"sourceId={Uri.EscapeDataString(sourceId)}" +
                $"&artifactVersionId={build.Id}" +
                $"&$top=1" +
                $"&queryOrder=descending" +
                $"&api-version=7.1";

            var releases = await GetFromApiAsync<ReleaseListResponse>(releaseListUrl);
            var releaseRef = releases?.Value?.FirstOrDefault();
            if (releaseRef == null)
            {
                return null;
            }

            // Get full release details
            return await GetReleaseAsync(releaseRef.Id);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to find release for build {BuildId}", build.Id);
            throw new InvalidOperationException($"Failed to find release for build {build.Id}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Finds a release triggered by a specific build ID.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <returns>The release, or null if not found.</returns>
    public async Task<ReleaseDetails?> FindReleaseForBuildIdAsync(int buildId)
    {
        var build = await GetBuildAsync(buildId);
        if (build == null)
        {
            return null;
        }

        return await FindReleaseForBuildAsync(build);
    }

    /// <summary>
    /// Batch finds releases for multiple builds with parallel execution.
    /// Optimized to reduce API calls by grouping builds by pipeline definition.
    /// </summary>
    /// <param name="builds">The builds to find releases for.</param>
    /// <param name="maxConcurrency">Maximum concurrent API calls (default 5).</param>
    /// <returns>A dictionary mapping build IDs to releases.</returns>
    public async Task<Dictionary<int, ReleaseDetails>> FindReleasesForBuildsAsync(IEnumerable<BuildDetails> builds, int maxConcurrency = 5)
    {
        var result = new Dictionary<int, ReleaseDetails>();

        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            var buildList = builds.Where(b => b.Definition?.Id != null).ToList();
            if (!buildList.Any())
            {
                return result;
            }

            // Group builds by pipeline definition to batch release queries
            var buildsByDefinition = buildList.GroupBy(b => b.Definition!.Id).ToList();

            // Use semaphore to limit concurrent API calls
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task<(int BuildId, ReleaseDetails? Release)>>();

            foreach (var group in buildsByDefinition)
            {
                var sourceId = $"{settings.ProjectId}:{group.Key}";

                foreach (var build in group)
                {
                    tasks.Add(FindReleaseForBuildWithSemaphoreAsync(semaphore, build, sourceId));
                }
            }

            var results = await Task.WhenAll(tasks);

            foreach (var (buildId, release) in results)
            {
                if (release != null)
                {
                    result[buildId] = release;
                }
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to find releases for builds");
            throw new InvalidOperationException($"Failed to find releases for builds: {ex.Message}", ex);
        }

        return result;
    }

    /// <summary>
    /// Gets the status of a release.
    /// </summary>
    /// <param name="releaseId">The release ID.</param>
    /// <returns>The release, or null if not found.</returns>
    public async Task<ReleaseDetails?> GetReleaseAsync(int releaseId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            var releaseUrl = $"{GetVsrmBaseUrl()}/_apis/release/releases/{releaseId}?api-version=7.1";
            return await GetFromApiAsync<ReleaseDetails>(releaseUrl);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to get release {ReleaseId}", releaseId);
            throw new InvalidOperationException($"Failed to get release {releaseId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if a release is complete for a specific stage.
    /// </summary>
    /// <param name="release">The release to check.</param>
    /// <param name="stageName">The stage/environment name to check. If null, checks all environments.</param>
    /// <param name="staleThreshold">Duration after which a NotStarted environment is considered stale. Default is 24 hours.</param>
    /// <returns>A tuple indicating if complete, if successful, and a message.</returns>
    public (bool IsComplete, bool Success, string Message) CheckReleaseCompletion(ReleaseDetails release, string? stageName = null, TimeSpan? staleThreshold = null)
    {
        staleThreshold ??= TimeSpan.FromHours(24);

        if (release.Status == ReleaseApiStatus.Abandoned)
        {
            return (true, false, "Release was abandoned");
        }

        if (release.Status != ReleaseApiStatus.Active)
        {
            return (false, false, "Release not active yet");
        }

        if (release.Environments?.Any() != true)
        {
            return (true, true, "No environments to track, release is active");
        }

        // Filter to specific stage if provided
        var environmentsToCheck = string.IsNullOrWhiteSpace(stageName)
            ? release.Environments.ToList()
            : release.Environments.Where(e => e.Name.Equals(stageName, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!environmentsToCheck.Any())
        {
            return (true, false, $"Stage '{stageName}' not found in release");
        }

        var allComplete = true;
        var anyFailed = false;
        var details = new List<string>();

        foreach (var env in environmentsToCheck)
        {
            // Environment is complete if in a terminal state
            var envComplete = env.Status == EnvironmentApiStatus.Succeeded ||
                              env.Status == EnvironmentApiStatus.Canceled ||
                              env.Status == EnvironmentApiStatus.Rejected ||
                              env.Status == EnvironmentApiStatus.PartiallySucceeded;

            // Check if environment is stale (NotStarted for too long - likely waiting for approval that won't come)
            var isStale = false;
            if (!envComplete && env.Status == EnvironmentApiStatus.NotStarted)
            {
                var releaseAge = DateTime.UtcNow - release.CreatedOn;
                if (releaseAge > staleThreshold)
                {
                    isStale = true;
                    envComplete = true; // Treat stale NotStarted as complete (but not successful)
                }
            }

            var envSucceeded = env.Status == EnvironmentApiStatus.Succeeded;

            if (!envComplete)
            {
                allComplete = false;
            }

            if (envComplete && !envSucceeded)
            {
                anyFailed = true;
            }

            var statusDisplay = isStale ? $"{env.Status}(stale)" : env.Status.ToString();
            details.Add($"{env.Name}:{statusDisplay}");
        }

        if (allComplete)
        {
            var message = anyFailed
                ? $"Stage(s) failed: [{string.Join(", ", details)}]"
                : $"Stage(s) succeeded: [{string.Join(", ", details)}]";
            return (true, !anyFailed, message);
        }

        return (false, false, $"Stage(s) still running: [{string.Join(", ", details)}]");
    }

    /// <summary>
    /// Gets deployment timing information from a specific release stage/environment.
    /// Uses actual deployment start/end times from the environment's deployment steps.
    /// </summary>
    /// <param name="release">The release to get timing from.</param>
    /// <param name="stageName">The stage/environment name to get timing for. If null, uses all environments.</param>
    /// <returns>A tuple with started time, completed time, and whether the release is complete.</returns>
    public (DateTime? StartedAt, DateTime? CompletedAt, bool IsComplete) GetReleaseDeploymentTiming(ReleaseDetails release, string? stageName = null)
    {
        if (release.Environments?.Any() != true)
        {
            return (release.CreatedOn, null, false);
        }

        // Filter to specific stage if provided
        var environmentsToCheck = string.IsNullOrWhiteSpace(stageName)
            ? release.Environments.ToList()
            : release.Environments.Where(e => e.Name.Equals(stageName, StringComparison.OrdinalIgnoreCase)).ToList();

        // Find environments that have actually started deployment
        var deployedEnvs = environmentsToCheck
            .Where(e => e.DeploySteps?.Any() == true)
            .ToList();

        if (!deployedEnvs.Any())
        {
            // No environments have started deployment yet
            return (release.CreatedOn, null, false);
        }

        // Get the earliest deployment start time from all deployment steps
        DateTime? earliestStart = null;
        DateTime? latestEnd = null;

        foreach (var env in deployedEnvs)
        {
            foreach (var deployStep in env.DeploySteps!)
            {
                // Get the first deploy phase attempt that actually ran
                var attempts = deployStep.ReleaseDeployPhases?
                    .SelectMany(p => p.DeploymentJobs ?? Enumerable.Empty<DTOs.DeploymentJob>())
                    .SelectMany(j => j.Tasks ?? Enumerable.Empty<DTOs.ReleaseTask>())
                    .ToList();

                if (attempts?.Any() == true)
                {
                    var stepStart = attempts.Min(t => t.StartTime);
                    var stepEnd = attempts
                        .Where(t => t.FinishTime.HasValue)
                        .Select(t => t.FinishTime)
                        .DefaultIfEmpty(null)
                        .Max();

                    if (stepStart.HasValue && (!earliestStart.HasValue || stepStart < earliestStart))
                    {
                        earliestStart = stepStart;
                    }

                    if (stepEnd.HasValue && (!latestEnd.HasValue || stepEnd > latestEnd))
                    {
                        latestEnd = stepEnd;
                    }
                }
            }
        }

        var (isComplete, _, _) = CheckReleaseCompletion(release, stageName);

        return (earliestStart ?? release.CreatedOn, isComplete ? latestEnd : null, isComplete);
    }

    /// <summary>
    /// Gets the build timeline (steps) for a build.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <returns>The timeline, or null if not found.</returns>
    public async Task<BuildTimeline?> GetBuildTimelineAsync(int buildId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                throw new InvalidOperationException("Failed to resolve project ID");
            }

            var url = $"{GetBuildBaseUrl()}/_apis/build/builds/{buildId}/timeline?api-version=7.1";
            return await GetFromApiAsync<BuildTimeline>(url);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger?.LogError(ex, "Failed to get build timeline for build {BuildId}", buildId);
            throw new InvalidOperationException($"Failed to get build timeline for build {buildId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the URL to view a build in Azure DevOps web UI.
    /// </summary>
    /// <param name="build">The build object.</param>
    /// <returns>The build URL.</returns>
    public string GetBuildUrl(BuildDetails build)
    {
        return $"{settings.OrganizationUrl}/{Uri.EscapeDataString(settings.ProjectName)}/_build/results?buildId={build.Id}";
    }

    /// <summary>
    /// Gets the URL to view build logs in Azure DevOps web UI.
    /// </summary>
    /// <param name="build">The build object.</param>
    /// <returns>The build logs URL.</returns>
    public string GetBuildLogsUrl(BuildDetails build)
    {
        return $"{GetBuildUrl(build)}&view=logs";
    }

    /// <summary>
    /// Gets the raw API URL for build logs.
    /// Format: https://dev.azure.com/{org}/{projectId}/_apis/build/builds/{buildId}/logs/{logId}.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <param name="logId">The specific log ID (optional - omit to get list of all logs).</param>
    /// <returns>The raw logs API URL.</returns>
    public string GetBuildLogsApiUrl(int buildId, int? logId = null)
    {
        var baseUrl = $"{settings.OrganizationUrl}/{settings.ProjectId}/_apis/build/builds/{buildId}/logs";
        return logId.HasValue ? $"{baseUrl}/{logId}" : baseUrl;
    }

    /// <summary>
    /// Gets the URL to view a release in Azure DevOps.
    /// </summary>
    /// <param name="releaseId">The release ID.</param>
    /// <returns>The release URL.</returns>
    public string GetReleaseUrl(int releaseId)
    {
        return $"{settings.OrganizationUrl}/{Uri.EscapeDataString(settings.ProjectName)}/_releaseProgress?_a=release-pipeline-progress&releaseId={releaseId}";
    }

    /// <summary>
    /// Gets the URL to view release logs for a specific environment in Azure DevOps.
    /// </summary>
    /// <param name="releaseId">The release ID.</param>
    /// <param name="environmentId">The environment ID (optional).</param>
    /// <returns>The release logs URL.</returns>
    public string GetReleaseLogsUrl(int releaseId, int? environmentId = null)
    {
        var baseUrl = $"{settings.OrganizationUrl}/{Uri.EscapeDataString(settings.ProjectName)}/_releaseProgress?releaseId={releaseId}&_a=release-logs";
        if (environmentId.HasValue)
        {
            baseUrl += $"&environmentId={environmentId}";
        }

        return baseUrl;
    }

    /// <summary>
    /// Gets the environment ID for a specific stage name in a release.
    /// </summary>
    /// <param name="release">The release.</param>
    /// <param name="stageName">The stage name.</param>
    /// <returns>The environment ID, or null if not found.</returns>
    public int? GetEnvironmentId(ReleaseDetails release, string? stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName) || release.Environments?.Any() != true)
        {
            return null;
        }

        return release.Environments
            .FirstOrDefault(e => e.Name.Equals(stageName, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                projectIdLock.Dispose();
                httpClient.Dispose();
            }

            disposed = true;
        }
    }

    private async Task<bool> EnsureProjectIdAsync()
    {
        if (!string.IsNullOrEmpty(settings.ProjectId))
        {
            return true;
        }

        await projectIdLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(settings.ProjectId))
            {
                return true;
            }

            return await TestConnectionAsync();
        }
        finally
        {
            projectIdLock.Release();
        }
    }

    /// <summary>
    /// Finds a release for a build with semaphore-controlled concurrency.
    /// </summary>
    private async Task<(int BuildId, ReleaseDetails? Release)> FindReleaseForBuildWithSemaphoreAsync(
        SemaphoreSlim semaphore,
        BuildDetails build,
        string sourceId)
    {
        await semaphore.WaitAsync();
        try
        {
            var releaseListUrl = $"{GetVsrmBaseUrl()}/_apis/release/releases?" +
                $"sourceId={Uri.EscapeDataString(sourceId)}" +
                $"&artifactVersionId={build.Id}" +
                $"&$top=1" +
                $"&queryOrder=descending" +
                $"&api-version=7.1";

            var releases = await GetFromApiAsync<ReleaseListResponse>(releaseListUrl);
            var releaseRef = releases?.Value?.FirstOrDefault();
            if (releaseRef == null)
            {
                return (build.Id, null);
            }

            var detailedRelease = await GetReleaseAsync(releaseRef.Id);
            return (build.Id, detailedRelease);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to find release for build {BuildId}", build.Id);
            return (build.Id, null);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Gets the base URL for Build API calls.
    /// </summary>
    /// <returns>The build API base URL.</returns>
    private string GetBuildBaseUrl()
    {
        return $"{settings.OrganizationUrl}/{Uri.EscapeDataString(settings.ProjectName)}";
    }

    /// <summary>
    /// Gets the VSRM (Visual Studio Release Management) base URL for Release API calls.
    /// Release APIs use a different subdomain than other Azure DevOps APIs.
    /// </summary>
    /// <returns>The VSRM base URL.</returns>
    private string GetVsrmBaseUrl()
    {
        // Azure DevOps Release APIs use vsrm.dev.azure.com instead of dev.azure.com
        var orgUrl = settings.OrganizationUrl;
        if (orgUrl.Contains("dev.azure.com"))
        {
            orgUrl = orgUrl.Replace("dev.azure.com", "vsrm.dev.azure.com");
        }
        else if (orgUrl.Contains(".visualstudio.com"))
        {
            // Legacy URLs: https://org.visualstudio.com -> https://org.vsrm.visualstudio.com
            orgUrl = orgUrl.Replace(".visualstudio.com", ".vsrm.visualstudio.com");
        }

        return $"{orgUrl}/{Uri.EscapeDataString(settings.ProjectName)}";
    }

    /// <summary>
    /// Makes an HTTP GET request to the Azure DevOps API and deserializes the response.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="url">The API URL.</param>
    /// <returns>The deserialized response, or default if failed.</returns>
    private async Task<T?> GetFromApiAsync<T>(string url)
        where T : class
    {
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, jsonOptions);
    }

    /// <summary>
    /// Makes an HTTP POST request to the Azure DevOps API and deserializes the response.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="url">The API URL.</param>
    /// <param name="content">The content to post.</param>
    /// <returns>The deserialized response, or default if failed.</returns>
    private async Task<T?> PostToApiAsync<T>(string url, object content)
        where T : class
    {
        var jsonContent = JsonSerializer.Serialize(content, jsonOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, httpContent);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, jsonOptions);
    }
}
