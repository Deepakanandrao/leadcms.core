// <copyright file="DeployPluginSettings.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.Deploy.Configuration;

/// <summary>
/// Root configuration for the Deploy plugin.
/// </summary>
public class DeployPluginSettings
{
    /// <summary>
    /// Gets or sets the Azure DevOps connection settings.
    /// </summary>
    public AzureDevOpsSettings AzureDevOps { get; set; } = new();

    /// <summary>
    /// Gets or sets the deployment targets configuration.
    /// Key is the target ID, value is the target configuration.
    /// </summary>
    public Dictionary<string, DeploymentTargetSettings> DeploymentTargets { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether the plugin is properly configured.
    /// </summary>
    public bool IsConfigured => AzureDevOps.IsConfigured && DeploymentTargets.Count > 0;
}

/// <summary>
/// Azure DevOps connection settings.
/// </summary>
public class AzureDevOpsSettings
{
    /// <summary>
    /// Gets or sets the Azure DevOps organization URL.
    /// Example: https://dev.azure.com/myorganization.
    /// </summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure DevOps project name.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Personal Access Token for authentication.
    /// IMPORTANT: This should be set via user secrets or environment variables, not in pluginsettings.json.
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the project ID (resolved at runtime from ProjectName).
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets a value indicating whether the Azure DevOps settings are configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(OrganizationUrl) &&
        !string.IsNullOrWhiteSpace(ProjectName) &&
        !string.IsNullOrWhiteSpace(PersonalAccessToken) &&
        !PersonalAccessToken.StartsWith('$');
}

/// <summary>
/// Configuration for a single deployment target.
/// </summary>
public class DeploymentTargetSettings
{
    /// <summary>
    /// Gets or sets the display name of the deployment target.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the deployment target.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the Azure DevOps build pipeline definition ID.
    /// </summary>
    public int BuildPipelineId { get; set; }

    /// <summary>
    /// Gets or sets the source branch to trigger the build with.
    /// Example: refs/heads/main.
    /// </summary>
    public string? SourceBranch { get; set; }

    /// <summary>
    /// Gets or sets the release stage/environment name to track after build completes.
    /// If set (e.g., "PROD", "PREVIEW", "NEXT"), deployment is considered complete when that specific stage succeeds.
    /// If null or empty, deployment is considered complete when build succeeds (release not tracked).
    /// </summary>
    public string? TrackReleaseStage { get; set; }

    /// <summary>
    /// Gets or sets the resource URL associated with this deployment target.
    /// Example: https://mysite.com.
    /// </summary>
    public string? Resource { get; set; }
}
