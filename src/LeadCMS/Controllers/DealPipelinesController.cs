// <copyright file="DealPipelinesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class DealPipelinesController : BaseController<DealPipeline, DealPipelineCreateDto, DealPipelineUpdateDto, DealPipelineDetailsDto>
{
    public DealPipelinesController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<DealPipeline> queryProviderFactory)
    : base(dbContext, mapper, esDbContext, queryProviderFactory)
    {
    }
}