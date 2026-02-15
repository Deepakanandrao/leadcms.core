// <copyright file="DomainsController.cs" company="WavePoint Co. Ltd.">
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

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class DomainsController : BaseControllerWithImport<Domain, DomainCreateDto, DomainUpdateDto, DomainDetailsDto, DomainImportDto>
{
    private readonly IDomainService domainService;

    public DomainsController(PgDbContext dbContext, IMapper mapper, IDomainService domainService, EsDbContext esDbContext, QueryProviderFactory<Domain> queryProviderFactory, ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.domainService = domainService;
    }

    // GET api/domains/names/gmail.com
    [HttpGet("verify/{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DomainDetailsDto>> Verify(string name, bool force = false)
    {
        var domain = (from d in dbSet
                      where d.Name == name
                      select d).FirstOrDefault();

        if (domain == null)
        {
            domain = new Domain() { Name = name };
            await domainService.SaveAsync(domain);
        }

        if (force)
        {
            domain.Title = null;
            domain.Description = null;
            domain.DnsRecords = null;
            domain.DnsCheck = null;
            domain.HttpCheck = null;
            domain.MxCheck = null;
            domain.Url = null;
        }

        await domainService.Verify(domain);
        await dbContext.SaveChangesAsync();

        var resultConverted = mapper.Map<DomainDetailsDto>(domain);

        return Ok(resultConverted);
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [ProducesResponseType(typeof(SyncResponseDto<DomainDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }

    protected override async Task SaveRangeAsync(List<Domain> newRecords)
    {
        await domainService.SaveRangeAsync(newRecords);
    }
}