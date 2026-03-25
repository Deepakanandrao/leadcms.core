// <copyright file="DealsController.cs" company="WavePoint Co. Ltd.">
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
public class DealsController : BaseController<Deal, DealCreateDto, DealUpdateDto, DealDetailsDto>
{
    private readonly IDealService dealService;

    public DealsController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<Deal> queryProviderFactory, IDealService dealService, ISyncService syncService)
    : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.dealService = dealService;
    }

    [HttpGet("tags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string[]>> GetTags()
    {
        var tags = TagsHelper.ToDistinctTags(await dbSet.Select(deal => deal.Tags).ToArrayAsync());
        return Ok(tags);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<DealDetailsDto>> Post([FromBody] DealCreateDto value)
    {
        var newValue = mapper.Map<Deal>(value);

        newValue.Contacts = GetContactsFromContactIds(value.ContactIds);

        await dealService.SaveAsync(newValue);

        await dbContext.SaveChangesAsync();

        var resultsToClient = mapper.Map<DealDetailsDto>(newValue);

        return CreatedAtAction(nameof(GetOne), new { id = newValue.Id }, resultsToClient);
    }

    public override async Task<ActionResult<DealDetailsDto>> Patch(int id, [FromBody] DealUpdateDto value)
    {
        var existingEntity = await FindOrThrowNotFound(id);

        mapper.Map(value, existingEntity);

        if (value.ContactIds != null)
        {
            dbContext.Entry(existingEntity).Collection(x => x.Contacts!).Load();
            existingEntity.Contacts = GetContactsFromContactIds(value.ContactIds);
        }

        await dealService.SaveAsync(existingEntity);

        await dbContext.SaveChangesAsync();

        var resultsToClient = mapper.Map<DealDetailsDto>(existingEntity);

        return Ok(resultsToClient);
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<DealDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }

    private List<Contact> GetContactsFromContactIds(HashSet<int> contactIds)
    {
        var contacts = dbContext.Contacts!.Where(c => contactIds.Contains(c.Id)).ToList();
        if (contacts.Count == contactIds.Count)
        {
            return contacts;
        }
        else
        {
            var existingContactIds = contacts.Select(c => c.Id);
            var nonExistingContactId = contactIds.First(cid => !existingContactIds.Contains(cid));
            throw new EntityNotFoundException("Contact", nonExistingContactId.ToString());
        }
    }
}