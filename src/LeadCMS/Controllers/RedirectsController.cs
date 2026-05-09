// <copyright file="RedirectsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class RedirectsController : BaseController<Redirect, RedirectCreateDto, RedirectUpdateDto, RedirectDetailsDto>
{
    private readonly IRedirectService redirectService;

    public RedirectsController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Redirect> queryProviderFactory,
        ISyncService syncService,
        IRedirectService redirectService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.redirectService = redirectService;
    }

    /// <summary>
    /// Creates a new redirect after validating uniqueness and cycle constraints.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public override async Task<ActionResult<RedirectDetailsDto>> Post([FromBody] RedirectCreateDto value)
    {
        await redirectService.ValidateAsync(value);
        return await base.Post(value);
    }

    /// <summary>
    /// Updates a redirect after validating uniqueness and cycle constraints.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public override async Task<ActionResult<RedirectDetailsDto>> Patch(int id, [FromBody] RedirectUpdateDto value)
    {
        var existing = await FindOrThrowNotFound(id);

        // Build post-patch state for validation by mapping the existing entity to a CreateDto,
        // then overlaying the incoming update fields (NullProperties handling is automatic via WithPatchDtoSupport).
        var merged = mapper.Map<RedirectCreateDto>(existing);
        mapper.Map(value, merged);

        await redirectService.ValidateAsync(merged, excludeId: id);
        return await Patch(existing, value);
    }

    /// <summary>
    /// Deletes a redirect. For auto-discovered redirects the entry is suppressed rather than
    /// physically deleted so that discovery does not recreate it on the next run.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> Delete(int id)
    {
        var entity = await FindOrThrowNotFound(id);

        if (entity.IsAutoDiscovered)
        {
            // Auto-discovered redirects are always ContentSlug type. Suppress instead of
            // physically deleting so the discovery process does not recreate the entry.
            entity.IsAutoDiscoverySuppressed = true;
            await dbContext.SaveChangesAsync();
        }
        else
        {
            dbContext.Remove(entity);
            await dbContext.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>
    /// Discovers redirects from Content change history, persists new ones to the database,
    /// and returns the stored redirects filtered and paged using the same query parameters
    /// as <c>GET /api/redirects</c>. Already manually created or edited redirects are not
    /// overwritten. Previously suppressed sources are not re-discovered.
    /// </summary>
    [HttpPost("discover")]
    [ProducesResponseType(typeof(List<RedirectDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RedirectDetailsDto>>> Discover()
    {
        await redirectService.DiscoverAsync();
        return await Get(null);
    }
}

