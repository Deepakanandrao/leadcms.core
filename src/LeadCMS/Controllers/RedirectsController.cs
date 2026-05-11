// <copyright file="RedirectsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Exceptions;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        if (existing.IsAutoDiscovered && HasFromOrToChange(value))
        {
            throw new UnprocessableEntityException(
                "Auto-discovered redirects are read-only except for the Kind field. " +
                "Delete the redirect to suppress it permanently.");
        }

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
            // Suppress instead of physically deleting so the discovery process does not
            // recreate the entry. The record is hidden from normal queries by the global
            // query filter on IsAutoDiscoverySuppressed.
            entity.IsAutoDiscoverySuppressed = true;
            await dbContext.SaveChangesAsync();

            // Manually record a Deleted ChangeLog entry so that sync clients see this
            // redirect as deleted. The automatic ChangeLog produced above is a Modified
            // entry (setting the suppression flag) which is invisible to the sync query.
            dbContext.ChangeLogs!.Add(new ChangeLog
            {
                ObjectType = nameof(Redirect),
                EntityState = EntityState.Deleted,
                CreatedAt = DateTime.UtcNow,
                ObjectId = entity.Id,
                Data = JsonHelper.Serialize(entity),
            });
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
    public async Task<ActionResult<List<RedirectDetailsDto>>> Discover([FromQuery] string? query)
    {
        await redirectService.DiscoverAsync();
        return await Get(query);
    }

    private static bool HasFromOrToChange(RedirectUpdateDto value)
    {
        var restrictedFields = new[]
        {
            nameof(RedirectUpdateDto.SourceType),
            nameof(RedirectUpdateDto.FromPath),
            nameof(RedirectUpdateDto.FromLanguage),
            nameof(RedirectUpdateDto.FromSlug),
            nameof(RedirectUpdateDto.FromContentId),
            nameof(RedirectUpdateDto.TargetType),
            nameof(RedirectUpdateDto.ToUrl),
            nameof(RedirectUpdateDto.ToPath),
            nameof(RedirectUpdateDto.ToLanguage),
            nameof(RedirectUpdateDto.ToSlug),
            nameof(RedirectUpdateDto.ToContentId),
        };

        return value.SourceType != null
            || value.FromPath != null
            || value.FromLanguage != null
            || value.FromSlug != null
            || value.FromContentId != null
            || value.TargetType != null
            || value.ToUrl != null
            || value.ToPath != null
            || value.ToLanguage != null
            || value.ToSlug != null
            || value.ToContentId != null
            || Array.Exists(restrictedFields, f => value.NullProperties.Contains(f));
    }
}

