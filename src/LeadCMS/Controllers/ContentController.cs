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
    private readonly ILogger<ContentController> logger;

    public ContentController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<Content> queryProviderFactory, CommentableControllerExtension commentableControllerExtension, IMediaResolver mediaResolver, IHttpContextHelper httpContextHelper, ILogger<ContentController> logger)
        : base(dbContext, mapper, esDbContext, queryProviderFactory)
    {
        this.commentableControllerExtension = commentableControllerExtension;
        this.mediaResolver = mediaResolver;
        this.httpContextHelper = httpContextHelper;
        this.logger = logger;
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
        var draftJson = JsonHelper.Serialize(draftDto);

        logger.LogInformation("[SSE] ========= Starting Draft Update ({Title}) ===========", draftDto.Title);

        // Get the current user ID (assuming claims-based identity)
        var currentUserId = await httpContextHelper.GetCurrentUserIdAsync();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        // Upsert into ContentDraft table, now unique per user, entity type, and entity id
        logger.LogInformation("[SSE] Attempting to upsert draft for Content Id={ContentId}, User={UserId}", existingEntity.Id, currentUserId);
        var existingDraft = await dbContext.ContentDrafts!
            .FirstOrDefaultAsync(d => d.ObjectType == "Content" && d.ObjectId == existingEntity.Id && d.CreatedById == currentUserId);

        if (existingDraft != null)
        {
            logger.LogInformation("[SSE] Updating existing draft for Content Id={ContentId}, User={UserId}", existingEntity.Id, currentUserId);
            existingDraft.Data = draftJson;
        }
        else
        {
            logger.LogInformation("[SSE] Creating new draft for Content Id={ContentId}, User={UserId}", existingEntity.Id, currentUserId);
            var draft = new ContentDraft
            {
                ObjectType = "Content",
                ObjectId = existingEntity.Id,
                Data = draftJson,
            };

            await dbContext.ContentDrafts!.AddAsync(draft);
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("[SSE] Draft saved and changes committed for Content Id={ContentId}, User={UserId}", existingEntity.Id, currentUserId);

        // Send PostgreSQL NOTIFY for draft changes
        try
        {
            logger.LogInformation("[SSE] Sending PostgreSQL NOTIFY draft_changes for Content Id={ContentId}, User={UserId}", existingEntity.Id, currentUserId);
            await dbContext.Database.ExecuteSqlRawAsync("NOTIFY draft_changes;");
        }
        catch (Exception ex)
        {
            // Log but do not fail the request
            logger.LogError(ex, "[SSE] Failed to send NOTIFY draft_changes for Content Id={ContentId}, User={UserId}", existingEntity.Id, currentUserId);
        }

        return Ok();
    }

    [HttpPost("draft")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SaveNewDraft([FromBody] ContentUpdateDto value)
    {
        // Get the current user ID (assuming claims-based identity)
        var currentUserId = await httpContextHelper.GetCurrentUserIdAsync();
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        // Serialize the draft DTO as JSON
        var draftJson = JsonHelper.Serialize(value);

        logger.LogInformation("[SSE] Attempting to upsert new draft (ObjectId=0) for User={UserId}", currentUserId);
        // Use ObjectId = 0 for new (unsaved) content drafts
        var existingDraft = await dbContext.ContentDrafts!
            .FirstOrDefaultAsync(d => d.ObjectType == "Content" && d.ObjectId == 0 && d.CreatedById == currentUserId);

        if (existingDraft != null)
        {
            logger.LogInformation("[SSE] Updating existing new draft (ObjectId=0) for User={UserId}", currentUserId);
            existingDraft.Data = draftJson;
            existingDraft.UpdatedAt = DateTime.UtcNow;
            existingDraft.UpdatedById = currentUserId;
        }
        else
        {
            logger.LogInformation("[SSE] Creating new draft (ObjectId=0) for User={UserId}", currentUserId);
            var draft = new ContentDraft
            {
                ObjectType = "Content",
                ObjectId = 0, // 0 means new/unsaved
                Data = draftJson,
            };
            
            await dbContext.ContentDrafts!.AddAsync(draft);
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("[SSE] New draft saved and changes committed (ObjectId=0) for User={UserId}", currentUserId);

        // Send PostgreSQL NOTIFY for draft changes
        try
        {
            logger.LogInformation("[SSE] Sending PostgreSQL NOTIFY draft_changes for new draft (ObjectId=0), User={UserId}", currentUserId);
            await dbContext.Database.ExecuteSqlRawAsync("NOTIFY draft_changes;");
        }
        catch (Exception ex)
        {
            // Log but do not fail the request
            logger.LogError(ex, "[SSE] Failed to send NOTIFY draft_changes for new draft (ObjectId=0), User={UserId}", currentUserId);
        }

        return Ok();
    }
}