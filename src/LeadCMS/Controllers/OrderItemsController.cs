// <copyright file="OrderItemsController.cs" company="WavePoint Co. Ltd.">
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
public class OrderItemsController : BaseControllerWithImport<OrderItem, OrderItemCreateDto, OrderItemUpdateDto, OrderItemDetailsDto, OrderItemImportDto>
{
    private readonly IOrderItemService orderItemService;

    public OrderItemsController(
        PgDbContext dbContext,
        IMapper mapper,
        IOrderItemService orderItemService,
        EsDbContext esDbContext,
        QueryProviderFactory<OrderItem> queryProviderFactory,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.orderItemService = orderItemService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<OrderItemDetailsDto>> Post([FromBody] OrderItemCreateDto value)
    {
        var order = await dbContext.Orders!
                            .Include(o => o.OrderItems)
                            .FirstOrDefaultAsync(o => o.Id == value.OrderId);

        if (order == null)
        {
            ModelState.AddModelError("OrderId", "The referenced order was not found");

            throw new InvalidModelStateException(ModelState);
        }

        var orderItem = mapper.Map<OrderItem>(value);
        orderItem.Order = order;

        await orderItemService.SaveAsync(orderItem);

        await dbContext.SaveChangesAsync();

        var returnedValue = mapper.Map<OrderItemDetailsDto>(orderItem);

        return CreatedAtAction(nameof(GetOne), new { id = orderItem.Id }, returnedValue);
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<OrderItemDetailsDto>> Patch(int id, [FromBody] OrderItemUpdateDto value)
    {
        var orderItem = await FindOrThrowNotFound(id);

        var order = await dbContext.Orders!
                            .Include(o => o.OrderItems)
                            .FirstOrDefaultAsync(o => o.Id == orderItem.OrderId);

        if (order == null)
        {
            ModelState.AddModelError("OrderId", "The referenced order was not found");

            throw new InvalidModelStateException(ModelState);
        }

        mapper.Map(value, orderItem);

        await orderItemService.SaveAsync(orderItem);

        await dbContext.SaveChangesAsync();

        return mapper.Map<OrderItemDetailsDto>(orderItem);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult> Delete(int id)
    {
        var orderItem = await FindOrThrowNotFound(id);

        await dbContext.Entry(orderItem)
                .Reference(oi => oi.Order)
                .LoadAsync();

        orderItemService.Delete(orderItem);

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("bulk")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult> DeleteMany([FromBody] List<int> ids)
    {
        return await BulkDeleteHelper.ExecuteAsync(
            dbContext,
            dbContext.OrderItems!.Include(orderItem => orderItem.Order),
            ids,
            customDelete: items => items.ForEach(orderItemService.Delete));
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<OrderItemDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }

    protected override async Task SaveRangeAsync(List<OrderItem> newRecords)
    {
        await orderItemService.SaveRangeAsync(newRecords);
    }
}