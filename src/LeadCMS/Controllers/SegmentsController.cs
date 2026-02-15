// <copyright file="SegmentsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class SegmentsController : BaseController<Segment, SegmentCreateDto, SegmentUpdateDto, SegmentDetailsDto>
{
    private readonly ISegmentService segmentService;

    public SegmentsController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Segment> queryProviderFactory,
        ISegmentService segmentService,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.segmentService = segmentService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SegmentDetailsDto>> Post([FromBody] SegmentCreateDto value)
    {
        var segment = mapper.Map<Segment>(value);

        await segmentService.SaveAsync(segment);

        await dbContext.SaveChangesAsync();

        var dto = mapper.Map<SegmentDetailsDto>(segment);
        return CreatedAtAction(nameof(GetOne), new { id = segment.Id }, dto);
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SegmentDetailsDto>> Patch(int id, [FromBody] SegmentUpdateDto value)
    {
        var segment = await FindOrThrowNotFound(id);

        mapper.Map(value, segment);

        await segmentService.SaveAsync(segment);

        await dbContext.SaveChangesAsync();

        var dto = mapper.Map<SegmentDetailsDto>(segment);
        return Ok(dto);
    }

    [HttpPost("preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SegmentPreviewResultDto>> Preview([FromBody] SegmentDefinition definition)
    {
        var result = await segmentService.PreviewSegmentAsync(definition, 100);
        return Ok(result);
    }

    [HttpGet("{id}/contacts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ContactDetailsDto>>> GetContacts(
        int id,
        [FromQuery] string? query = null,
        [FromQuery] int? limit = null)
    {
        var contacts = await segmentService.GetSegmentContactsAsync(id, query, limit);

        var contactDtos = mapper.Map<List<ContactDetailsDto>>(contacts);
        contactDtos.ForEach(c =>
        {
            c.AvatarUrl = GravatarHelper.EmailToGravatarUrl(c.Email);
        });

        Response.Headers.Append(ResponseHeaderNames.TotalCount, contacts.Count.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(contactDtos);
    }

    [HttpPost("{id}/recalculate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SegmentDetailsDto>> Recalculate(int id)
    {
        await segmentService.RecalculateContactCountAsync(id);

        var segment = await FindOrThrowNotFound(id);
        var dto = mapper.Map<SegmentDetailsDto>(segment);

        return Ok(dto);
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<SegmentDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }
}
