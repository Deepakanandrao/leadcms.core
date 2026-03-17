// <copyright file="SequencesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Core.Sequences.DTOs;
using LeadCMS.Core.Sequences.Interfaces;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.Sequences.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class SequencesController : LeadCMS.Controllers.BaseController<Sequence, SequenceCreateDto, SequenceUpdateDto, SequenceDetailsDto>
{
    private readonly ISequenceService sequenceService;

    public SequencesController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Sequence> queryProviderFactory,
        ISequenceService sequenceService,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.sequenceService = sequenceService;
    }

    /// <summary>
    /// Gets a sequence with all its steps.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SequenceDetailsDto>> GetOne(int id)
    {
        var sequence = await sequenceService.GetFullAsync(id);
        var dto = mapper.Map<SequenceDetailsDto>(sequence);
        return Ok(dto);
    }

    /// <summary>
    /// Creates a new sequence with steps. Status is always Draft on creation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SequenceDetailsDto>> Post([FromBody] SequenceCreateDto value)
    {
        var sequence = await sequenceService.SaveFullAsync(null, value);
        var dto = mapper.Map<SequenceDetailsDto>(sequence);
        return CreatedAtAction(nameof(GetOne), new { id = sequence.Id }, dto);
    }

    /// <summary>
    /// Replaces a sequence and its steps entirely.
    /// Sequence must be in Draft or Paused status.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SequenceDetailsDto>> Put(int id, [FromBody] SequenceCreateDto value)
    {
        var sequence = await sequenceService.SaveFullAsync(id, value);
        var dto = mapper.Map<SequenceDetailsDto>(sequence);
        return Ok(dto);
    }

    /// <summary>
    /// Partially updates a sequence. Only Draft or Paused sequences can be edited.
    /// If steps are provided, the step list is reconciled by step ID.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<SequenceDetailsDto>> Patch(int id, [FromBody] SequenceUpdateDto value)
    {
        var existingEntity = await FindOrThrowNotFound(id);

        if (existingEntity.Status != SequenceStatus.Draft && existingEntity.Status != SequenceStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Sequence can only be edited in Draft or Paused status. Current status: {existingEntity.Status}.");
        }

        var steps = value.Steps;
        value.Steps = null;

        await Patch(existingEntity, value);

        if (steps != null)
        {
            await sequenceService.ReplaceStepsAsync(id, steps);
        }

        var sequence = await sequenceService.GetFullAsync(id);
        var dto = mapper.Map<SequenceDetailsDto>(sequence);
        return Ok(dto);
    }

    /// <summary>
    /// Activates a sequence (Draft or Paused → Active).
    /// </summary>
    [HttpPost("{id}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SequenceDetailsDto>> Activate(int id)
    {
        var sequence = await sequenceService.ActivateAsync(id);
        var dto = mapper.Map<SequenceDetailsDto>(sequence);
        return Ok(dto);
    }

    /// <summary>
    /// Pauses an active sequence.
    /// </summary>
    [HttpPost("{id}/pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SequenceDetailsDto>> Pause(int id)
    {
        var sequence = await sequenceService.PauseAsync(id);
        var dto = mapper.Map<SequenceDetailsDto>(sequence);
        return Ok(dto);
    }

    /// <summary>
    /// Archives a sequence and exits all active enrollments.
    /// </summary>
    [HttpPost("{id}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SequenceDetailsDto>> Archive(int id)
    {
        var sequence = await sequenceService.ArchiveAsync(id);
        var dto = mapper.Map<SequenceDetailsDto>(sequence);
        return Ok(dto);
    }

    /// <summary>
    /// Gets sequence statistics.
    /// </summary>
    [HttpGet("{id}/statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SequenceStatisticsDto>> GetStatistics(int id)
    {
        var stats = await sequenceService.GetStatisticsAsync(id);
        return Ok(stats);
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<SequenceDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }
}
