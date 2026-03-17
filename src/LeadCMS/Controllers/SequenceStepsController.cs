// <copyright file="SequenceStepsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/sequences/{sequenceId}/steps")]
public class SequenceStepsController : ControllerBase
{
    private readonly PgDbContext dbContext;
    private readonly IMapper mapper;

    public SequenceStepsController(PgDbContext dbContext, IMapper mapper)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    /// <summary>
    /// Lists all steps for a sequence, ordered by position.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SequenceStepDetailsDto>>> Get(int sequenceId)
    {
        await FindSequenceOrThrow(sequenceId);

        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequenceId)
            .OrderBy(s => s.Position)
            .ToListAsync();

        return Ok(mapper.Map<List<SequenceStepDetailsDto>>(steps));
    }

    /// <summary>
    /// Gets a single step by ID.
    /// </summary>
    [HttpGet("{stepId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SequenceStepDetailsDto>> GetOne(int sequenceId, int stepId)
    {
        await FindSequenceOrThrow(sequenceId);

        var step = await dbContext.SequenceSteps!
            .FirstOrDefaultAsync(s => s.Id == stepId && s.SequenceId == sequenceId)
            ?? throw new EntityNotFoundException(nameof(SequenceStep), stepId.ToString());

        return Ok(mapper.Map<SequenceStepDetailsDto>(step));
    }

    /// <summary>
    /// Creates a new step. Automatically assigns position if not provided.
    /// Sequence must be in Draft or Paused status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SequenceStepDetailsDto>> Post(int sequenceId, [FromBody] SequenceStepCreateDto value)
    {
        var sequence = await FindSequenceOrThrow(sequenceId);
        ValidateSequenceEditable(sequence);

        var step = mapper.Map<SequenceStep>(value);
        step.SequenceId = sequenceId;

        if (!value.Position.HasValue)
        {
            var maxPosition = await dbContext.SequenceSteps!
                .Where(s => s.SequenceId == sequenceId)
                .MaxAsync(s => (int?)s.Position) ?? 0;
            step.Position = maxPosition + 1;
        }

        await dbContext.SequenceSteps!.AddAsync(step);
        await dbContext.SaveChangesAsync();

        var dto = mapper.Map<SequenceStepDetailsDto>(step);
        return CreatedAtAction(nameof(GetOne), new { sequenceId, stepId = step.Id }, dto);
    }

    /// <summary>
    /// Updates a step. Sequence must be in Draft or Paused status.
    /// </summary>
    [HttpPatch("{stepId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SequenceStepDetailsDto>> Patch(int sequenceId, int stepId, [FromBody] SequenceStepUpdateDto value)
    {
        var sequence = await FindSequenceOrThrow(sequenceId);
        ValidateSequenceEditable(sequence);

        var step = await dbContext.SequenceSteps!
            .FirstOrDefaultAsync(s => s.Id == stepId && s.SequenceId == sequenceId)
            ?? throw new EntityNotFoundException(nameof(SequenceStep), stepId.ToString());

        mapper.Map(value, step);
        await dbContext.SaveChangesAsync();

        return Ok(mapper.Map<SequenceStepDetailsDto>(step));
    }

    /// <summary>
    /// Deletes a step and reorders remaining steps. Sequence must be in Draft or Paused status.
    /// </summary>
    [HttpDelete("{stepId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Delete(int sequenceId, int stepId)
    {
        var sequence = await FindSequenceOrThrow(sequenceId);
        ValidateSequenceEditable(sequence);

        var step = await dbContext.SequenceSteps!
            .FirstOrDefaultAsync(s => s.Id == stepId && s.SequenceId == sequenceId)
            ?? throw new EntityNotFoundException(nameof(SequenceStep), stepId.ToString());

        dbContext.SequenceSteps!.Remove(step);

        // Reorder remaining steps
        var laterSteps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequenceId && s.Position > step.Position)
            .OrderBy(s => s.Position)
            .ToListAsync();

        foreach (var laterStep in laterSteps)
        {
            laterStep.Position--;
        }

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Reorders steps by providing the full list of step IDs in desired order.
    /// Sequence must be in Draft or Paused status.
    /// </summary>
    [HttpPost("reorder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<SequenceStepDetailsDto>>> Reorder(int sequenceId, [FromBody] SequenceStepReorderDto value)
    {
        var sequence = await FindSequenceOrThrow(sequenceId);
        ValidateSequenceEditable(sequence);

        var steps = await dbContext.SequenceSteps!
            .Where(s => s.SequenceId == sequenceId)
            .ToListAsync();

        if (value.StepIds.Length != steps.Count)
        {
            throw new InvalidOperationException(
                $"Expected {steps.Count} step IDs but received {value.StepIds.Length}.");
        }

        for (var i = 0; i < value.StepIds.Length; i++)
        {
            var step = steps.FirstOrDefault(s => s.Id == value.StepIds[i])
                ?? throw new EntityNotFoundException(nameof(SequenceStep), value.StepIds[i].ToString());
            step.Position = i + 1;
        }

        await dbContext.SaveChangesAsync();

        var orderedSteps = steps.OrderBy(s => s.Position).ToList();
        return Ok(mapper.Map<List<SequenceStepDetailsDto>>(orderedSteps));
    }

    private static void ValidateSequenceEditable(Sequence sequence)
    {
        if (sequence.Status != SequenceStatus.Draft && sequence.Status != SequenceStatus.Paused)
        {
            throw new InvalidOperationException(
                $"Steps can only be modified when the sequence is in Draft or Paused status. Current status: {sequence.Status}.");
        }
    }

    private async Task<Sequence> FindSequenceOrThrow(int sequenceId)
    {
        return await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());
    }
}
