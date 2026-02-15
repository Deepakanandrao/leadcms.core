// <copyright file="OrdersController.cs" company="WavePoint Co. Ltd.">
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
public class OrdersController : BaseControllerWithImport<Order, OrderCreateDto, OrderUpdateDto, OrderDetailsDto, OrderImportDto>
{
    private readonly IOrderService orderService;
    private readonly CommentableControllerExtension commentableControllerExtension;

    public OrdersController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Order> queryProviderFactory,
        CommentableControllerExtension commentableControllerExtension,
        IOrderService orderService,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.commentableControllerExtension = commentableControllerExtension;
        this.orderService = orderService;
    }

    [AllowAnonymous]
    [HttpGet("{id}/comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CommentDetailsDto>>> GetComments(int id)
    {
        return commentableControllerExtension.ReturnComments(await commentableControllerExtension.GetCommentsForICommentable<Order>(id), this);
    }

    [HttpGet("currencies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<string>>> GetCurrencies()
    {
        var currencies = await dbSet
            .Where(order => !order.TestOrder)
            .Select(order => order.Currency)
            .Distinct()
            .OrderBy(currency => currency)
            .ToListAsync();

        return Ok(currencies);
    }

    [AllowAnonymous]
    [HttpPost("{id}/comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CommentDetailsDto>> PostComment(int id, [FromBody] CommentCreateBaseDto value)
    {
        return await commentableControllerExtension.PostComment(commentableControllerExtension.CreateCommentForICommentable<Order>(value, id), this);
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<OrderDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }

    protected override async Task SaveRangeAsync(List<Order> newRecords)
    {
        await orderService.SaveRangeAsync(newRecords);
    }
}