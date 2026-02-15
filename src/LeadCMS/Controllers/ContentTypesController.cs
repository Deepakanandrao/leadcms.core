// <copyright file="ContentTypesController.cs" company="WavePoint Co. Ltd.">
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
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class ContentTypesController : BaseControllerWithImport<ContentType, ContentTypeCreateDto, ContentTypeUpdateDto, ContentTypeDetailsDto, ContentTypeImportDto>
{
    public ContentTypesController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<ContentType> queryProviderFactory, ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<List<ContentTypeDetailsDto>>> Get([FromQuery] string? query)
    {
        // Get the base result without content counts
        var baseResult = await base.Get(query);

        if (baseResult.Result is OkObjectResult okResult && okResult.Value is List<ContentTypeDetailsDto> contentTypes)
        {
            await EnrichWithContentCounts(contentTypes);
        }

        return baseResult;
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ContentTypeDetailsDto>> GetOne(int id)
    {
        // Get the base result without content count
        var baseResult = await base.GetOne(id);

        if (baseResult.Result is OkObjectResult okResult && okResult.Value is ContentTypeDetailsDto contentType)
        {
            await EnrichWithContentCounts(new List<ContentTypeDetailsDto> { contentType });
        }

        return baseResult;
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<ContentTypeDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }

    /// <summary>
    /// Enriches the ContentType DTOs with the count of related content records.
    /// </summary>
    /// <param name="contentTypes">The list of ContentType DTOs to enrich.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task EnrichWithContentCounts(List<ContentTypeDetailsDto> contentTypes)
    {
        if (contentTypes.Count == 0)
        {
            return;
        }

        // Get all ContentType UIDs for the current batch
        var contentTypeUids = contentTypes.Select(ct => ct.Uid).ToList();

        // Query the database to get content counts for each content type
        var contentCounts = await dbContext.Set<Content>()
            .Where(c => contentTypeUids.Contains(c.Type))
            .GroupBy(c => c.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        // Create a dictionary for fast lookup
        var contentCountDict = contentCounts.ToDictionary(cc => cc.Type, cc => cc.Count);

        // Enrich each ContentType DTO with its content count
        foreach (var contentType in contentTypes)
        {
            contentType.ContentCount = contentCountDict.GetValueOrDefault(contentType.Uid, 0);
        }
    }
}
