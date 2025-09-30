// <copyright file="LinksController.cs" company="WavePoint Co. Ltd.">
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

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class LinksController : BaseControllerWithImport<Link, LinkCreateDto, LinkUpdateDto, LinkDetailsDto, LinkImportDto>
{
    public LinksController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Link> queryProviderFactory,
        ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
    }

    // POST api/links
    [HttpPost]
    public override Task<ActionResult<LinkDetailsDto>> Post([FromBody] LinkCreateDto link)
    {
        if (string.IsNullOrEmpty(link.Uid))
        {
            link.Uid = UidHelper.Generate();
        }

        return base.Post(link);
    }
}