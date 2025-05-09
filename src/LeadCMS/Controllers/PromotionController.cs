// <copyright file="PromotionController.cs" company="WavePoint Co. Ltd.">
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
public class PromotionController : BaseController<Promotion, PromotionCreateDto, PromotionUpdateDto, PromotionDetailsDto>
{
    public PromotionController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<Promotion> queryProviderFactory)
        : base(dbContext, mapper, esDbContext, queryProviderFactory)
    {
    }
}