// <copyright file="IDeploymentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Deployments.DTOs;

namespace LeadCMS.Core.Deployments.Interfaces;

public interface IDeploymentService
{
    /// <summary>
    /// Gets all configured deployment targets.
    /// </summary>
    /// <returns>A list of deployment targets.</returns>
    Task<List<DeploymentTargetDto>> GetTargetsAsync();

    /// <summary>
    /// Gets recent deployment records.
    /// </summary>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <returns>A list of recent deployment records.</returns>
    Task<List<DeploymentRecordDto>> GetRecentDeploymentsAsync(int limit = 20);

    /// <summary>
    /// Gets detailed information about a specific deployment, including steps and logs if available.
    /// </summary>
    /// <param name="deploymentId">The deployment identifier.</param>
    /// <returns>The deployment details, or null if not found.</returns>
    Task<DeploymentDetailsDto?> GetDeploymentAsync(string deploymentId);

    /// <summary>
    /// Gets deployment statistics.
    /// </summary>
    /// <returns>Deployment statistics.</returns>
    Task<DeploymentStatsDto> GetStatsAsync();

    /// <summary>
    /// Triggers a single deployment target.
    /// </summary>
    /// <param name="targetId">The target identifier to trigger.</param>
    /// <param name="triggeredById">The ID of the user triggering the deployment.</param>
    /// <returns>The result of the trigger operation.</returns>
    Task<DeploymentTriggerResultDto> TriggerAsync(string targetId, string? triggeredById);

    /// <summary>
    /// Triggers multiple deployment targets.
    /// </summary>
    /// <param name="targetIds">The target identifiers to trigger.</param>
    /// <param name="triggeredById">The ID of the user triggering the deployments.</param>
    /// <returns>The result of the trigger operation.</returns>
    Task<DeploymentTriggerResultDto> TriggerAsync(IEnumerable<string> targetIds, string? triggeredById);

    /// <summary>
    /// Triggers all configured deployment targets.
    /// </summary>
    /// <param name="triggeredById">The ID of the user triggering the deployments.</param>
    /// <returns>The result of the trigger operation.</returns>
    Task<DeploymentTriggerResultDto> TriggerAllAsync(string? triggeredById);
}
