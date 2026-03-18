// <copyright file="SequenceDeliveriesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Core.Sequences.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Core.Sequences.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/sequences/{sequenceId}/deliveries")]
public class SequenceDeliveriesController : ControllerBase
{
    private readonly IMapper mapper;
    private readonly QueryProviderFactory<SequenceDelivery> queryProviderFactory;

    public SequenceDeliveriesController(
        IMapper mapper,
        QueryProviderFactory<SequenceDelivery> queryProviderFactory)
    {
        this.mapper = mapper;
        this.queryProviderFactory = queryProviderFactory;
    }

    /// <summary>
    /// Lists deliveries for a sequence with support for filtering, pagination, and search.
    /// Search terms are matched against contact attributes (email, name, company, etc.).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SequenceDeliveryDetailsDto>>> Get(
        int sequenceId,
        [FromQuery] string? query)
    {
        var qp = queryProviderFactory.BuildQueryProvider(
            additionalQueryString: $"filter[where][SequenceId]={sequenceId}");
        var result = await qp.GetResult();

        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(mapper.Map<List<SequenceDeliveryDetailsDto>>(result.Records));
    }

    /// <summary>
    /// Gets a single delivery by ID.
    /// </summary>
    [HttpGet("{deliveryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SequenceDeliveryDetailsDto>> GetOne(int sequenceId, int deliveryId)
    {
        var qp = queryProviderFactory.BuildQueryProvider(
            limit: 1,
            additionalQueryString: $"filter[where][SequenceId]={sequenceId}&filter[where][id]={deliveryId}");
        var result = await qp.GetResult();

        if (result.Records == null || result.Records.Count == 0)
        {
            throw new EntityNotFoundException(nameof(SequenceDelivery), deliveryId.ToString());
        }

        return Ok(mapper.Map<SequenceDeliveryDetailsDto>(result.Records.First()));
    }
}
