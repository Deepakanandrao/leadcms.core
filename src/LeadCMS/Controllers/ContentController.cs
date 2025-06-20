// <copyright file="ContentController.cs" company="WavePoint Co. Ltd.">
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
public class ContentController : BaseControllerWithImport<Content, ContentCreateDto, ContentUpdateDto, ContentDetailsDto, ContentImportDto>
{
    private readonly CommentableControllerExtension commentableControllerExtension;
    private readonly IMediaResolver mediaResolver;

    public ContentController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<Content> queryProviderFactory, CommentableControllerExtension commentableControllerExtension, IMediaResolver mediaResolver)
        : base(dbContext, mapper, esDbContext, queryProviderFactory)
    {
        this.commentableControllerExtension = commentableControllerExtension;
        this.mediaResolver = mediaResolver;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<List<ContentDetailsDto>>> Get([FromQuery] string? query)
    {
        var result = await base.Get(query);
        var mode = MediaResolutionHelper.GetResolutionMode(HttpContext);

        // If result is OkObjectResult, extract the value
        if (result.Result is OkObjectResult okResult && okResult.Value is List<ContentDetailsDto> list)
        {
            if (mode == "absolute")
            {
                foreach (var item in list)
                {
                    item.CoverImageUrl = mediaResolver.Resolve(item.CoverImageUrl, HttpContext, mode);
                    item.Body = MediaUriTransformer.Transform(item.Body, mediaResolver, HttpContext, mode);
                }
            }

            return Ok(list);
        }

        // fallback for other result types
        return result;
    }

    // GET api/{entity}s/5
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ContentDetailsDto>> GetOne(int id)
    {
        var result = await base.GetOne(id);
        var mode = MediaResolutionHelper.GetResolutionMode(HttpContext);

        if (result.Result is OkObjectResult okResult && okResult.Value is ContentDetailsDto dto)
        {
            if (mode == "absolute")
            {
                dto.Body = MediaUriTransformer.Transform(dto.Body, mediaResolver, HttpContext, mode);
                dto.CoverImageUrl = mediaResolver.Resolve(dto.CoverImageUrl, HttpContext, mode);
            }
            
            return Ok(dto);
        }

        return result;
    }

    [HttpGet("tags")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string[]>> GetTags()
    {
        var tags = (await dbSet.Select(c => c.Tags).ToArrayAsync()).SelectMany(z => z).Distinct().Where(str => !string.IsNullOrEmpty(str)).ToArray();
        return Ok(tags);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]

    public async Task<ActionResult<string[]>> GetCategories()
    {
        var categories = (await dbSet.Select(c => c.Category).ToArrayAsync()).Distinct().Where(str => !string.IsNullOrEmpty(str)).ToArray();
        return Ok(categories);
    }

    [HttpGet("{id}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CommentDetailsDto>>> GetComments(int id)
    {
        return commentableControllerExtension.ReturnComments(await commentableControllerExtension.GetCommentsForICommentable<Content>(id), this);
    }

    [HttpPost("{id}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CommentDetailsDto>> PostComment(int id, [FromBody] CommentCreateBaseDto value)
    {
        return await commentableControllerExtension.PostComment(commentableControllerExtension.CreateCommentForICommentable<Content>(value, id), this);
    }

    [HttpGet("sync")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }
}