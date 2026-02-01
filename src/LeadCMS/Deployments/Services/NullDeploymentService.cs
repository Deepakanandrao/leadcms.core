// <copyright file="NullDeploymentService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.Deployments.DTOs;
using LeadCMS.Core.Deployments.Exceptions;
using LeadCMS.Core.Deployments.Interfaces;

namespace LeadCMS.Core.Deployments.Services;

/// <summary>
/// Default deployment service used when no deployment plugin is configured.
/// Returns empty results for read operations and throws for trigger operations.
/// </summary>
public class NullDeploymentService : IDeploymentService
{
    public Task<List<DeploymentTargetDto>> GetTargetsAsync()
    {
        throw new DeploymentNotConfiguredException();
    }

    public Task<List<DeploymentRecordDto>> GetRecentDeploymentsAsync(int limit = 20)
    {
        throw new DeploymentNotConfiguredException();
    }

    public Task<DeploymentDetailsDto?> GetDeploymentAsync(string deploymentId)
    {
        throw new DeploymentNotConfiguredException();
    }

    public Task<DeploymentStatsDto> GetStatsAsync()
    {
        throw new DeploymentNotConfiguredException();
    }

    public Task<DeploymentTriggerResultDto> TriggerAsync(string targetId, string? triggeredById)
    {
        throw new DeploymentNotConfiguredException();
    }

    public Task<DeploymentTriggerResultDto> TriggerAsync(IEnumerable<string> targetIds, string? triggeredById)
    {
        throw new DeploymentNotConfiguredException();
    }

    public Task<DeploymentTriggerResultDto> TriggerAllAsync(string? triggeredById)
    {
        throw new DeploymentNotConfiguredException();
    }
}
