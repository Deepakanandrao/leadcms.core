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
using Microsoft.EntityFrameworkCore;

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

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SegmentDetailsDto>> GetOne(int id)
    {
        var segment = await dbContext.Segments!
            .Where(s => s.Id == id)
            .FirstOrDefaultAsync();

        if (segment == null)
        {
            throw new EntityNotFoundException("Segment", id.ToString());
        }

        var dto = mapper.Map<SegmentDetailsDto>(segment);
        return Ok(dto);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<List<SegmentDetailsDto>>> Get([FromQuery] string? query)
    {
        return await base.Get(query);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SegmentDetailsDto>> Post([FromBody] SegmentCreateDto value)
    {
        var segment = mapper.Map<Segment>(value);

        // Validate segment
        await segmentService.ValidateSegmentAsync(segment);

        // Calculate initial contact count
        segment.ContactCount = await segmentService.CalculateContactCountAsync(segment);

        await dbSet.AddAsync(segment);
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
        var segment = await dbContext.Segments!
            .Where(s => s.Id == id)
            .FirstOrDefaultAsync();

        if (segment == null)
        {
            throw new EntityNotFoundException("Segment", id.ToString());
        }

        mapper.Map(value, segment);

        // Validate segment
        await segmentService.ValidateSegmentAsync(segment);

        // Recalculate contact count
        segment.ContactCount = await segmentService.CalculateContactCountAsync(segment);

        await dbContext.SaveChangesAsync();

        var dto = mapper.Map<SegmentDetailsDto>(segment);
        return Ok(dto);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult> Delete(int id)
    {
        return await base.Delete(id);
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
}
