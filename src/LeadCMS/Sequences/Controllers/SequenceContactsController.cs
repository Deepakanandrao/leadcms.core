// <copyright file="SequenceContactsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.Sequences.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/sequences/{sequenceId}/contacts")]
public class SequenceContactsController : ControllerBase
{
    private readonly PgDbContext dbContext;
    private readonly IMapper mapper;
    private readonly QueryProviderFactory<Contact> queryProviderFactory;

    public SequenceContactsController(
        PgDbContext dbContext,
        IMapper mapper,
        QueryProviderFactory<Contact> queryProviderFactory)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
        this.queryProviderFactory = queryProviderFactory;
    }

    /// <summary>
    /// Lists distinct contacts enrolled in a sequence, with support for filtering, pagination, and search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ContactDetailsDto>>> Get(
        int sequenceId,
        [FromQuery] string? query)
    {
        _ = await dbContext.Sequences!.FindAsync(sequenceId)
            ?? throw new EntityNotFoundException(nameof(Sequence), sequenceId.ToString());

        var contactIds = await dbContext.SequenceEnrollments!
            .Where(e => e.SequenceId == sequenceId)
            .Select(e => e.ContactId)
            .Distinct()
            .ToListAsync();

        var idsFilter = contactIds.Count > 0
            ? string.Join(',', contactIds)
            : "-1";

        var qp = queryProviderFactory.BuildQueryProvider(
            additionalQueryString: $"filter[ids]={idsFilter}");
        var result = await qp.GetResult();

        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(mapper.Map<List<ContactDetailsDto>>(result.Records));
    }
}
