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
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace LeadCMS.Plugin.Deploy.Services;

/// <summary>
/// Client for Azure DevOps API operations.
/// Thread-safe - can be used as a singleton across multiple requests.
/// Uses HttpClient for Release API to avoid SDK IdentityDescriptor deserialization issues.
/// </summary>
public class AzureDevOpsClient : IDisposable
{
    private readonly AzureDevOpsSettings settings;
    private readonly ILogger<AzureDevOpsClient>? logger;
    private readonly object connectionLock = new();
    private readonly SemaphoreSlim projectIdLock = new(1, 1);
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions;
    private VssConnection? connection;
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

        // Initialize HttpClient for Release API calls (avoids SDK deserialization issues)
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
            var conn = GetConnection();
            var projectClient = conn.GetClient<ProjectHttpClient>();

            var projects = await projectClient.GetProjects();
            var targetProject = projects.FirstOrDefault(p =>
                string.Equals(p.Name, settings.ProjectName, StringComparison.OrdinalIgnoreCase));

            if (targetProject == null)
            {
                return false;
            }

            settings.ProjectId = targetProject.Id.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Triggers a build pipeline.
    /// </summary>
    /// <param name="definitionId">The build definition ID.</param>
    /// <param name="sourceBranch">Optional source branch.</param>
    /// <returns>The queued build, or null if failed.</returns>
    public async Task<Build?> TriggerBuildAsync(int definitionId, string? sourceBranch = null)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return null;
            }

            var conn = GetConnection();
            var buildClient = conn.GetClient<BuildHttpClient>();

            var buildDefinition = await buildClient.GetDefinitionAsync(
                project: settings.ProjectId!,
                definitionId: definitionId);

            var buildRequest = new Build
            {
                Definition = buildDefinition,
                Project = new TeamProjectReference
                {
                    Name = settings.ProjectName,
                    Id = Guid.Parse(settings.ProjectId!),
                },
                SourceBranch = sourceBranch ?? buildDefinition.Repository?.DefaultBranch ?? "refs/heads/main",
            };

            return await buildClient.QueueBuildAsync(buildRequest);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to trigger build for definition {DefinitionId} on branch {SourceBranch}", definitionId, sourceBranch);
            throw new InvalidOperationException($"Failed to trigger build for definition {definitionId}", ex);
        }
    }

    /// <summary>
    /// Gets the status of a build.
    /// </summary>
    /// <param name="buildId">The build ID.</param>
    /// <returns>The build, or null if not found.</returns>
    public async Task<Build?> GetBuildAsync(int buildId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return null;
            }

            var conn = GetConnection();
            var buildClient = conn.GetClient<BuildHttpClient>();

            return await buildClient.GetBuildAsync(settings.ProjectId!, buildId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get build {BuildId}", buildId);
            throw new InvalidOperationException($"Failed to get build {buildId}", ex);
        }
    }

    /// <summary>
    /// Gets the build definition.
    /// </summary>
    /// <param name="definitionId">The build definition ID.</param>
    /// <returns>The build definition, or null if not found.</returns>
    public async Task<BuildDefinition?> GetBuildDefinitionAsync(int definitionId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return null;
            }

            var conn = GetConnection();
            var buildClient = conn.GetClient<BuildHttpClient>();

            return await buildClient.GetDefinitionAsync(settings.ProjectId!, definitionId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get build definition {DefinitionId}", definitionId);
            throw new InvalidOperationException($"Failed to get build definition {definitionId}", ex);
        }
    }

    /// <summary>
    /// Gets recent builds for the specified pipeline definitions.
    /// </summary>
    /// <param name="definitionIds">The build definition IDs.</param>
    /// <param name="top">Maximum number of builds to return.</param>
    /// <returns>A list of builds.</returns>
    public async Task<List<Build>> GetRecentBuildsAsync(IEnumerable<int> definitionIds, int top = 20)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return new List<Build>();
            }

            var conn = GetConnection();
            var buildClient = conn.GetClient<BuildHttpClient>();

            var builds = await buildClient.GetBuildsAsync(
                project: settings.ProjectId!,
                definitions: definitionIds.ToArray(),
                queryOrder: BuildQueryOrder.StartTimeDescending,
                top: top);

            return builds.ToList();
        }
        catch
        {
            return new List<Build>();
        }
    }

    /// <summary>
    /// Gets builds with specific statuses.
    /// </summary>
    /// <param name="definitionIds">The build definition IDs.</param>
    /// <param name="status">The build status to filter.</param>
    /// <param name="top">Maximum number of builds to return.</param>
    /// <returns>A list of builds.</returns>
    public async Task<List<Build>> GetBuildsWithStatusAsync(IEnumerable<int> definitionIds, BuildStatus status, int top = 50)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return new List<Build>();
            }

            var conn = GetConnection();
            var buildClient = conn.GetClient<BuildHttpClient>();

            var builds = await buildClient.GetBuildsAsync(
                project: settings.ProjectId!,
                definitions: definitionIds.ToArray(),
                statusFilter: status,
                queryOrder: BuildQueryOrder.StartTimeDescending,
                top: top);

            return builds.ToList();
        }
        catch
        {
            return new List<Build>();
        }
    }

    /// <summary>
    /// Finds a release triggered by a specific build using the artifact source ID filter.
    /// This is more reliable than time-based filtering and works for historical builds.
    /// Uses HTTP REST API to avoid SDK IdentityDescriptor deserialization issues.
    /// </summary>
    /// <param name="build">The build to find releases for.</param>
    /// <returns>The release, or null if not found.</returns>
    public async Task<ReleaseDetails?> FindReleaseForBuildAsync(Build build)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return null;
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
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to find release for build {BuildId}", build.Id);
            throw new InvalidOperationException($"Failed to find release for build {build.Id}", ex);
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
    /// Batch finds releases for multiple builds.
    /// Uses HTTP REST API to avoid SDK IdentityDescriptor deserialization issues.
    /// </summary>
    /// <param name="builds">The builds to find releases for.</param>
    /// <returns>A dictionary mapping build IDs to releases.</returns>
    public async Task<Dictionary<int, ReleaseDetails>> FindReleasesForBuildsAsync(IEnumerable<Build> builds)
    {
        var result = new Dictionary<int, ReleaseDetails>();

        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return result;
            }

            // Use the same reliable logic as FindReleaseForBuildAsync for each build
            foreach (var build in builds.Where(b => b.Definition?.Id != null))
            {
                try
                {
                    var sourceId = $"{settings.ProjectId}:{build.Definition!.Id}";

                    var releaseListUrl = $"{GetVsrmBaseUrl()}/_apis/release/releases?" +
                        $"sourceId={Uri.EscapeDataString(sourceId)}" +
                        $"&artifactVersionId={build.Id}" +
                        $"&$top=1" +
                        $"&queryOrder=descending" +
                        $"&api-version=7.1";

                    var releases = await GetFromApiAsync<ReleaseListResponse>(releaseListUrl);
                    var releaseRef = releases?.Value?.FirstOrDefault();
                    if (releaseRef != null)
                    {
                        var detailedRelease = await GetReleaseAsync(releaseRef.Id);
                        if (detailedRelease != null)
                        {
                            result[build.Id] = detailedRelease;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to find release for build {BuildId}", build.Id);
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to find releases for builds");
            // Return whatever we collected
        }

        return result;
    }

    /// <summary>
    /// Gets the status of a release.
    /// Uses HTTP REST API to avoid SDK IdentityDescriptor deserialization issues.
    /// </summary>
    /// <param name="releaseId">The release ID.</param>
    /// <returns>The release, or null if not found.</returns>
    public async Task<ReleaseDetails?> GetReleaseAsync(int releaseId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return null;
            }

            var releaseUrl = $"{GetVsrmBaseUrl()}/_apis/release/releases/{releaseId}?api-version=7.1";
            return await GetFromApiAsync<ReleaseDetails>(releaseUrl);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get release {ReleaseId}", releaseId);
            throw new InvalidOperationException($"Failed to get release {releaseId}", ex);
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
    public async Task<Timeline?> GetBuildTimelineAsync(int buildId)
    {
        try
        {
            if (!await EnsureProjectIdAsync())
            {
                return null;
            }

            var conn = GetConnection();
            var buildClient = conn.GetClient<BuildHttpClient>();

            return await buildClient.GetBuildTimelineAsync(settings.ProjectId!, buildId);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get build timeline for build {BuildId}", buildId);
            throw new InvalidOperationException($"Failed to get build timeline for build {buildId}", ex);
        }
    }

    /// <summary>
    /// Gets the URL to view a build in Azure DevOps.
    /// </summary>
    /// <param name="build">The build object.</param>
    /// <returns>The build URL.</returns>
    public string? GetBuildUrl(Build build)
    {
        return build.Links?.Links.TryGetValue("web", out var webLink) == true
            ? (webLink as Microsoft.VisualStudio.Services.WebApi.ReferenceLink)?.Href
            : null;
    }

    /// <summary>
    /// Gets the URL to view build logs in Azure DevOps.
    /// </summary>
    /// <param name="build">The build object.</param>
    /// <returns>The build logs URL.</returns>
    public string? GetBuildLogsUrl(Build build)
    {
        var baseUrl = GetBuildUrl(build);
        return !string.IsNullOrEmpty(baseUrl) ? $"{baseUrl}&view=logs" : null;
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
                connection?.Dispose();
                projectIdLock.Dispose();
                httpClient.Dispose();
            }

            disposed = true;
        }
    }

    private VssConnection GetConnection()
    {
        if (connection != null)
        {
            return connection;
        }

        lock (connectionLock)
        {
            // Double-check after acquiring lock
            if (connection == null)
            {
                var credentials = new VssBasicCredential(string.Empty, settings.PersonalAccessToken);
                connection = new VssConnection(new Uri(settings.OrganizationUrl), credentials);
            }

            return connection;
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
}
