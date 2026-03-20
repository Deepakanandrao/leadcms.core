// <copyright file="SequenceEnrollmentsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Core.Sequences.DTOs;
using LeadCMS.Core.Sequences.Interfaces;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Core.Sequences.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/sequences/{sequenceId}/enrollments")]
public class SequenceEnrollmentsController : ControllerBase
{
    private readonly IMapper mapper;
    private readonly ISequenceService sequenceService;
    private readonly QueryProviderFactory<SequenceEnrollment> queryProviderFactory;

    public SequenceEnrollmentsController(
        IMapper mapper,
        ISequenceService sequenceService,
        QueryProviderFactory<SequenceEnrollment> queryProviderFactory)
    {
        this.mapper = mapper;
        this.sequenceService = sequenceService;
        this.queryProviderFactory = queryProviderFactory;
    }

    /// <summary>
    /// Lists enrollments for a sequence with support for filtering, pagination, and search.
    /// Search terms are matched against contact attributes (email, name, company, etc.).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SequenceEnrollmentDetailsDto>>> Get(
        int sequenceId,
        [FromQuery] string? query)
    {
        var qp = queryProviderFactory.BuildQueryProvider(
            additionalQueryString: $"filter[where][SequenceId]={sequenceId}");
        var result = await qp.GetResult();

        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(mapper.Map<List<SequenceEnrollmentDetailsDto>>(result.Records));
    }

    /// <summary>
    /// Gets a single enrollment by ID, including executed, scheduled, and planned steps.
    /// </summary>
    [HttpGet("{enrollmentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SequenceEnrollmentDetailsDto>> GetOne(int sequenceId, int enrollmentId)
    {
        var dto = await sequenceService.GetEnrollmentWithTimelineAsync(sequenceId, enrollmentId);
        return Ok(dto);
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
        var templateArguments = value.TemplateArguments?.ToDictionary(kv => kv.Key, kv => (object)kv.Value);

        var enrollments = await sequenceService.EnrollContactsAsync(
            sequenceId, value.ContactIds, value.EnrollmentReason, templateArguments, SequenceEnrollmentSource.Manual);

        var dtos = mapper.Map<List<SequenceEnrollmentDetailsDto>>(enrollments);
        return StatusCode(StatusCodes.Status201Created, dtos);
    }

    /// <summary>
    /// Stops selected enrollments with a given exit reason.
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<SequenceEnrollmentDetailsDto>>> Stop(
        int sequenceId,
        [FromBody] SequenceEnrollmentStopDto value)
    {
        var enrollments = await sequenceService.StopEnrollmentsAsync(
            sequenceId, value.EnrollmentIds);

        return Ok(mapper.Map<List<SequenceEnrollmentDetailsDto>>(enrollments));
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
