// <copyright file="DeploymentsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Claims;
using System.Security.Cryptography;
using LeadCMS.Constants;
using LeadCMS.Core.Deployments.DTOs;
using LeadCMS.Core.Deployments.Interfaces;
using LeadCMS.Core.Deployments.Services;
using LeadCMS.Exceptions;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Core.Deployments.Controllers;

[ApiController]
[Route("api/deployments")]
[Authorize]
public class DeploymentsController : ControllerBase
{
    private readonly IDeploymentService deploymentService;
    private readonly ISettingService settingService;

    public DeploymentsController(ISettingService settingService, IDeploymentService? deploymentService = null)
    {
        this.settingService = settingService;
        this.deploymentService = deploymentService ?? new NullDeploymentService();
    }

    /// <summary>
    /// Gets all configured deployment targets.
    /// </summary>
    [HttpGet("targets")]
    [ProducesResponseType(typeof(List<DeploymentTargetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DeploymentTargetDto>>> GetTargets()
    {
        var targets = await deploymentService.GetTargetsAsync();
        return Ok(targets);
    }

    /// <summary>
    /// Gets recent deployment records.
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 20).</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeploymentRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DeploymentRecordDto>>> GetRecentDeployments([FromQuery] int limit = 20)
    {
        var deployments = await deploymentService.GetRecentDeploymentsAsync(limit);
        return Ok(deployments);
    }

    /// <summary>
    /// Gets detailed information about a specific deployment.
    /// </summary>
    /// <param name="id">The deployment identifier.</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DeploymentDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeploymentDetailsDto>> GetDeployment(string id)
    {
        var deployment = await deploymentService.GetDeploymentAsync(id);

        if (deployment == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Deployment not found",
                Detail = $"Deployment with ID '{id}' was not found.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        return Ok(deployment);
    }

    /// <summary>
    /// Gets deployment statistics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(DeploymentStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeploymentStatsDto>> GetStats()
    {
        var stats = await deploymentService.GetStatsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Triggers deployment(s) based on the request parameters.
    /// </summary>
    /// <param name="request">The trigger request specifying target(s) or trigger-all flag.</param>
    /// <remarks>
    /// Examples:
    /// - Trigger single target: { "targetIds": ["target1"] }.
    /// - Trigger multiple targets: { "targetIds": ["target1", "target2"] }.
    /// - Trigger all targets: { "triggerAll": true }.
    /// </remarks>
    [HttpPost("trigger")]
    [ProducesResponseType(typeof(DeploymentTriggerResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DeploymentTriggerResultDto>> TriggerDeployments([FromBody] DeploymentTriggerRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        DeploymentTriggerResultDto result;

        if (request.TriggerAll)
        {
            // Trigger all configured targets
            result = await deploymentService.TriggerAllAsync(userId);
        }
        else if (request.TargetIds != null && request.TargetIds.Count > 0)
        {
            // Trigger specific target(s)
            if (request.TargetIds.Count == 1)
            {
                result = await deploymentService.TriggerAsync(request.TargetIds[0], userId);
            }
            else
            {
                result = await deploymentService.TriggerAsync(request.TargetIds, userId);
            }
        }
        else
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Either provide at least one target ID or set triggerAll to true.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Webhook for external CI/CD pipelines to notify of a completed deployment.
    /// Authenticates via the <c>X-Deployment-Api-Key</c> header against
    /// the <c>Deployment.WebhooksApiKey</c> setting and updates
    /// <c>Deployment.LastSuccessDate</c> to the current UTC time.
    /// </summary>
    /// <param name="deploymentApiKey">The deployment API key configured in settings.</param>
    [HttpPost("notify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> NotifyDeployment(
        [FromHeader(Name = "X-Deployment-Api-Key")] string? deploymentApiKey)
    {
        if (string.IsNullOrWhiteSpace(deploymentApiKey))
        {
            throw new UnauthorizedException("The X-Deployment-Api-Key header is required.");
        }

        var configuredKey = await settingService.GetSystemSettingAsync(SettingKeys.DeploymentWebhooksApiKey);

        if (string.IsNullOrWhiteSpace(configuredKey) ||
            !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(deploymentApiKey!),
                System.Text.Encoding.UTF8.GetBytes(configuredKey)))
        {
            throw new UnauthorizedException("The provided deployment API key is not valid.");
        }

        await settingService.SetSystemSettingAsync(
            SettingKeys.DeploymentLastSuccessDate,
            DateTime.UtcNow.ToString("o"));

        return Ok();
    }
}
