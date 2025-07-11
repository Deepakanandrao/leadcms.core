// <copyright file="RedirectsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Route("api/[controller]")]
public class RedirectsController : ControllerBase
{
    private readonly IRedirectService redirectService;
    private readonly ILogger<RedirectsController> logger;

    public RedirectsController(IRedirectService redirectService, ILogger<RedirectsController> logger)
    {
        this.redirectService = redirectService;
        this.logger = logger;
    }

    /// <summary>
    /// Get all auto-discovered redirects based on Content change history.
    /// </summary>
    /// <returns>List of redirects that should be implemented to handle changed content paths.</returns>
    [HttpGet("discover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RedirectDetailsDto>>> GetAutoDiscoveredRedirects()
    {
        try
        {
            logger.LogInformation("Discovering redirects from Content change history");

            var redirects = await redirectService.DiscoverRedirectsAsync();

            logger.LogInformation("Found {Count} discovered redirects", redirects.Count);

            return Ok(redirects);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while fetching discovered redirects");
            throw;
        }
    }
}
