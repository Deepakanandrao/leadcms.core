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
    private readonly IHttpContextHelper httpContextHelper;

    public ContentController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<Content> queryProviderFactory, CommentableControllerExtension commentableControllerExtension, IMediaResolver mediaResolver, IHttpContextHelper httpContextHelper)
        : base(dbContext, mapper, esDbContext, queryProviderFactory)
    {
        this.commentableControllerExtension = commentableControllerExtension;
        this.mediaResolver = mediaResolver;
        this.httpContextHelper = httpContextHelper;
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

    [HttpPatch("{id}/draft")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> PatchDraft(int id, [FromBody] ContentUpdateDto value)
    {
        var existingEntity = await FindOrThrowNotFound(id);

        // Create a copy of the entity to avoid mutating the tracked entity
        var draftEntity = mapper.Map<Content>(mapper.Map<ContentUpdateDto>(existingEntity));
        mapper.Map(value, draftEntity); // apply patch to the copy

        // Map the draft entity to ContentDetailsDto
        var draftDto = mapper.Map<ContentDetailsDto>(draftEntity);

        // Serialize the mapped DTO as JSON
        var draftJson = System.Text.Json.JsonSerializer.Serialize(draftDto);

        // Get the current user ID (assuming claims-based identity)
        var currentUserId = await httpContextHelper.GetCurrentUserIdAsync();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        // Upsert into ContentDraft table, now unique per user, entity type, and entity id
        var existingDraft = await dbContext.ContentDrafts!
            .FirstOrDefaultAsync(d => d.ObjectType == "Content" && d.ObjectId == existingEntity.Id && d.CreatedById == currentUserId);

        if (existingDraft != null)
        {
            existingDraft.Data = draftJson;
        }
        else
        {
            var draft = new ContentDraft
            {
                ObjectType = "Content",
                ObjectId = existingEntity.Id,
                Data = draftJson,
            };

            await dbContext.ContentDrafts!.AddAsync(draft);
        }

        // Send PostgreSQL NOTIFY for draft changes
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("NOTIFY draft_changes;");
        }
        catch (Exception ex)
        {
            // Log but do not fail the request
            Console.WriteLine($"Failed to send NOTIFY draft_changes: {ex.Message}");
        }

        await dbContext.SaveChangesAsync();

        return Ok();
    }
}