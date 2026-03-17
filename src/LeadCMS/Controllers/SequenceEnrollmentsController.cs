// <copyright file="SequenceEnrollmentsController.cs" company="WavePoint Co. Ltd.">
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
[Route("api/sequences/{sequenceId}/enrollments")]
public class SequenceEnrollmentsController : ControllerBase
{
    private readonly PgDbContext dbContext;
    private readonly IMapper mapper;
    private readonly ISequenceService sequenceService;

    public SequenceEnrollmentsController(PgDbContext dbContext, IMapper mapper, ISequenceService sequenceService)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
        this.sequenceService = sequenceService;
    }

    /// <summary>
    /// Lists enrollments for a sequence with optional status filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SequenceEnrollmentDetailsDto>>> Get(
        int sequenceId,
        [FromQuery] SequenceEnrollmentStatus? status)
    {
        _ = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        var query = dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequenceId);

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var enrollments = await query
            .OrderByDescending(e => e.EnteredAt)
            .ToListAsync();

        Response.Headers.Append(ResponseHeaderNames.TotalCount, enrollments.Count.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(mapper.Map<List<SequenceEnrollmentDetailsDto>>(enrollments));
    }

    /// <summary>
    /// Gets a single enrollment by ID.
    /// </summary>
    [HttpGet("{enrollmentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SequenceEnrollmentDetailsDto>> GetOne(int sequenceId, int enrollmentId)
    {
        var enrollment = await dbContext.SequenceEnrollments!
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.SequenceId == sequenceId)
            ?? throw new EntityNotFoundException(nameof(SequenceEnrollment), enrollmentId.ToString());

        return Ok(mapper.Map<SequenceEnrollmentDetailsDto>(enrollment));
    }

    /// <summary>
    /// Enrolls contacts into a sequence.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<SequenceEnrollmentDetailsDto>>> Post(
        int sequenceId,
        [FromBody] SequenceEnrollmentCreateDto value)
    {
        var enrollments = await sequenceService.EnrollContactsAsync(
            sequenceId, value.ContactIds, value.EnrollmentReason, SequenceEnrollmentSource.Manual);

        var dtos = mapper.Map<List<SequenceEnrollmentDetailsDto>>(enrollments);
        return StatusCode(StatusCodes.Status201Created, dtos);
    }

    /// <summary>
    /// Removes a contact from a sequence (exits their enrollment).
    /// </summary>
    [HttpDelete("{enrollmentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SequenceEnrollmentDetailsDto>> Delete(int sequenceId, int enrollmentId)
    {
        var enrollment = await sequenceService.RemoveEnrollmentAsync(sequenceId, enrollmentId);
        return Ok(mapper.Map<SequenceEnrollmentDetailsDto>(enrollment));
    }
}
