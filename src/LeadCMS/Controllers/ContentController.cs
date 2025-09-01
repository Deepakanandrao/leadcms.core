// <copyright file="ContentController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
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
    private readonly ITranslationService translationService;
    private readonly IMediaResolver mediaResolver;
    private readonly IHttpContextHelper httpContextHelper;
    private readonly ILogger<ContentController> logger;
    private readonly IMdxComponentParserService mdxComponentParserService;

    public ContentController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<Content> queryProviderFactory, CommentableControllerExtension commentableControllerExtension, ITranslationService translationService, IMediaResolver mediaResolver, IHttpContextHelper httpContextHelper, ILogger<ContentController> logger, IMdxComponentParserService mdxComponentParserService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory)
    {
        this.commentableControllerExtension = commentableControllerExtension;
        this.translationService = translationService;
        this.mediaResolver = mediaResolver;
        this.httpContextHelper = httpContextHelper;
        this.logger = logger;
        this.mdxComponentParserService = mdxComponentParserService;
    }

    /// <summary>
    /// Creates new content and automatically clears MDX component cache for the content type.
    /// </summary>
    public override async Task<ActionResult<ContentDetailsDto>> Post([FromBody] ContentCreateDto value)
    {
        var result = await base.Post(value);

        // Clear cache for the content type if it's an MDX type
        if (result.Result is CreatedAtActionResult createdResult && 
            createdResult.Value is ContentDetailsDto contentDto && 
            !string.IsNullOrEmpty(contentDto.Type))
        {
            await ClearCacheIfMdxType(contentDto.Type);
        }

        return result;
    }

    /// <summary>
    /// Updates existing content and automatically clears MDX component cache for the content type.
    /// </summary>
    public override async Task<ActionResult<ContentDetailsDto>> Patch(int id, [FromBody] ContentUpdateDto value)
    {
        // Get the existing content to know the type
        var existingContent = await FindOrThrowNotFound(id);
        
        var result = await base.Patch(id, value);

        // Clear cache for the content type if it's an MDX type
        if (!string.IsNullOrEmpty(existingContent.Type))
        {
            await ClearCacheIfMdxType(existingContent.Type);
        }

        return result;
    }

    /// <summary>
    /// Deletes content and automatically clears MDX component cache for the content type.
    /// </summary>
    public override async Task<ActionResult> Delete(int id)
    {
        // Get the existing content to know the type before deletion
        var existingContent = await FindOrThrowNotFound(id);
        var contentType = existingContent.Type;

        var result = await base.Delete(id);

        // Clear cache for the content type if it's an MDX type
        if (!string.IsNullOrEmpty(contentType))
        {
            await ClearCacheIfMdxType(contentType);
        }

        return result;
    }

    /// <summary>
    /// Imports content and automatically clears MDX component cache for affected content types.
    /// </summary>
    public override async Task<ActionResult<ImportResult>> Import([FromBody] List<ContentImportDto> importRecords)
    {
        var result = await base.Import(importRecords);

        // Get unique content types from the import records and clear cache for MDX types
        var contentTypes = importRecords.Where(r => !string.IsNullOrEmpty(r.Type))
                                      .Select(r => r.Type!)
                                      .Distinct();

        foreach (var contentType in contentTypes)
        {
            await ClearCacheIfMdxType(contentType);
        }

        return result;
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

    [HttpGet("{id}/translation-draft/{language}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ContentDetailsDto>> GetTranslationDraft(int id, string language, [FromQuery] TranslationTransformerType transformer = TranslationTransformerType.EmptyCopy)
    {
        var translationDraft = await translationService.CreateTranslationDraftAsync<Content>(id, language, transformer);
        var draftDto = mapper.Map<ContentDetailsDto>(translationDraft);

        return Ok(draftDto);
    }

    [HttpGet("{id}/translations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ContentDetailsDto>>> GetTranslations(int id)
    {
        var translations = await translationService.GetTranslationsAsync<Content>(id);
        var translationDtos = mapper.Map<List<ContentDetailsDto>>(translations);

        return Ok(translationDtos);
    }

    [HttpGet("mdx-components/{contentType}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MdxComponentAnalysisDto>> GetMdxComponents(string contentType, [FromQuery] bool useCache = true, [FromQuery] int? maxCacheAgeHours = 1)
    {
        try
        {
            MdxComponentAnalysisDto? result = null;

            // Try to get cached results first if requested
            if (useCache && maxCacheAgeHours.HasValue)
            {
                var maxAge = TimeSpan.FromHours(maxCacheAgeHours.Value);
                result = await mdxComponentParserService.GetCachedAnalysisAsync(contentType, maxAge);
            }

            // If no cached result, perform analysis
            if (result == null)
            {
                result = await mdxComponentParserService.AnalyzeContentTypeAsync(contentType);
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Content Type Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze MDX components for content type: {ContentType}", contentType);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Analysis Failed",
                Detail = "An error occurred while analyzing MDX components",
                Status = StatusCodes.Status500InternalServerError,
            });
        }
    }

    /// <summary>
    /// Clears the MDX component cache for a content type if it's an MDX format type.
    /// </summary>
    private async Task ClearCacheIfMdxType(string contentType)
    {
        try
        {
            // Check if the content type is MDX format
            var contentTypeEntity = await dbContext.ContentTypes!
                .Where(ct => ct.Uid == contentType && ct.Format == ContentFormat.MDX)
                .FirstOrDefaultAsync();

            if (contentTypeEntity != null)
            {
                logger.LogInformation("Clearing MDX component cache for content type: {ContentType}", contentType);
                await mdxComponentParserService.ClearCacheAsync(contentType);
                logger.LogDebug("Successfully cleared MDX component cache for content type: {ContentType}", contentType);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the main operation
            logger.LogWarning(ex, "Failed to clear MDX component cache for content type: {ContentType}", contentType);
        }
    }
}