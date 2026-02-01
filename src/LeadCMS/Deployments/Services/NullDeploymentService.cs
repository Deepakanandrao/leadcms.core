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
        return Task.FromResult(new List<DeploymentTargetDto>());
    }

    public Task<List<DeploymentRecordDto>> GetRecentDeploymentsAsync(int limit = 20)
    {
        return Task.FromResult(new List<DeploymentRecordDto>());
    }

    public Task<DeploymentDetailsDto?> GetDeploymentAsync(string deploymentId)
    {
        return Task.FromResult<DeploymentDetailsDto?>(null);
    }

    public Task<DeploymentStatsDto> GetStatsAsync()
    {
        return Task.FromResult(new DeploymentStatsDto
        {
            TotalDeployments = 0,
            SuccessfulDeployments = 0,
            FailedDeployments = 0,
            PendingDeployments = 0,
            InProgressDeployments = 0,
            SuccessRate = 0,
            AverageDuration = null,
        });
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
