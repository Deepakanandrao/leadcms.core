// <copyright file="ContentController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using AutoMapper;
using LeadCMS.Attributes;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using LeadCMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class ContentController : BaseControllerWithImport<Content, ContentCreateDto, ContentUpdateDto, ContentDetailsDto, ContentImportDto>
{
    private static readonly Regex MediaPathRegex = new Regex(@"/api/media/[^\s""')]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly CommentableControllerExtension commentableControllerExtension;
    private readonly ITranslationService translationService;
    private readonly IMediaResolver mediaResolver;
    private readonly IHttpContextHelper httpContextHelper;
    private readonly ILogger<ContentController> logger;
    private readonly IMdxComponentParserService mdxComponentParserService;
    private readonly ILanguageValidationService languageValidationService;
    private readonly IChangeLogService changeLogService;
    private readonly IOptions<ApiSettingsConfig> apiSettingsConfig;
    private readonly IMediaUsageService mediaUsageService;

    public ContentController(
        PgDbContext dbContext,
        IMapper mapper,
        EsDbContext esDbContext,
        QueryProviderFactory<Content> queryProviderFactory,
        CommentableControllerExtension commentableControllerExtension,
        ITranslationService translationService,
        IMediaResolver mediaResolver,
        IHttpContextHelper httpContextHelper,
        ILogger<ContentController> logger,
        IMdxComponentParserService mdxComponentParserService,
        ILanguageValidationService languageValidationService,
        IChangeLogService changeLogService,
        IOptions<ApiSettingsConfig> apiSettingsConfig,
        ISyncService syncService,
        IMediaUsageService mediaUsageService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.commentableControllerExtension = commentableControllerExtension;
        this.translationService = translationService;
        this.mediaResolver = mediaResolver;
        this.httpContextHelper = httpContextHelper;
        this.logger = logger;
        this.mdxComponentParserService = mdxComponentParserService;
        this.languageValidationService = languageValidationService;
        this.changeLogService = changeLogService;
        this.apiSettingsConfig = apiSettingsConfig;
        this.mediaUsageService = mediaUsageService;
    }

    [HttpGet]
    [AllowAnonymous]
    [IncludeTranslationsParameter(Description = "Include translation mappings in the response")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<List<ContentDetailsDto>>> Get([FromQuery] string? query)
    {
        // Check for includeTranslations parameter manually from query string since we can't change the base method signature
        var includeTranslations = Request.Query.ContainsKey("includeTranslations") &&
                                 bool.TryParse(Request.Query["includeTranslations"], out var parsed) && parsed;

        var result = await base.Get(query);
        var mode = MediaResolutionHelper.GetResolutionMode(HttpContext);

        // If result is OkObjectResult, extract the value
        if (result.Result is OkObjectResult okResult && okResult.Value is List<ContentDetailsDto> list)
        {
            if (mode == "absolute")
            {
                foreach (var item in list)
                {
                    if (!string.IsNullOrWhiteSpace(item.CoverImageUrl))
                    {
                        item.CoverImageUrl = mediaResolver.Resolve(item.CoverImageUrl, HttpContext, mode);
                    }

                    item.Body = MediaUriTransformer.Transform(item.Body, mediaResolver, HttpContext, mode);
                }
            }

            if (includeTranslations)
            {
                await PopulateTranslationsAsync(list);
            }

            return Ok(list);
        }

        // fallback for other result types
        return result;
    }

    [HttpGet("with-statistics")]
    [AllowAnonymous]
    [IncludeTranslationsParameter(Description = "Include translation mappings in the response")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ContentWithStatisticsDto>> GetWithStatistics([FromQuery] string? query)
    {
        // Check for includeTranslations parameter manually from query string
        var includeTranslations = Request.Query.ContainsKey("includeTranslations") &&
                                 bool.TryParse(Request.Query["includeTranslations"], out var parsed) && parsed;

        // Get the content using the base method
        var returnedItems = (await base.Get(query)).Result;
        var items = (List<ContentDetailsDto>)((ObjectResult)returnedItems!).Value!;

        // Get statistics for all content types using query without type filter
        var allStatistics = await GetContentTypeStatisticsWithQuery();

        // Process items (resolve media URLs if needed)
        var mode = MediaResolutionHelper.GetResolutionMode(HttpContext);
        if (mode == "absolute")
        {
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.CoverImageUrl))
                {
                    item.CoverImageUrl = mediaResolver.Resolve(item.CoverImageUrl, HttpContext, mode);
                }

                item.Body = MediaUriTransformer.Transform(item.Body, mediaResolver, HttpContext, mode);
            }
        }

        if (includeTranslations)
        {
            await PopulateTranslationsAsync(items);
        }

        var result = new ContentWithStatisticsDto
        {
            Content = items,
            Statistics = allStatistics,
        };

        return Ok(result);
    }

    // GET api/{entity}s/5
    [HttpGet("{id}")]
    [AllowAnonymous]
    [IncludeTranslationsParameter(Description = "Include translation mappings in the response")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<ContentDetailsDto>> GetOne(int id)
    {
        // Check for includeTranslations parameter manually from query string
        var includeTranslations = Request.Query.ContainsKey("includeTranslations") &&
                                 bool.TryParse(Request.Query["includeTranslations"], out var parsed) && parsed;

        var result = await base.GetOne(id);
        var mode = MediaResolutionHelper.GetResolutionMode(HttpContext);

        if (result.Result is OkObjectResult okResult && okResult.Value is ContentDetailsDto dto)
        {
            if (mode == "absolute")
            {
                dto.Body = MediaUriTransformer.Transform(dto.Body, mediaResolver, HttpContext, mode);

                if (!string.IsNullOrWhiteSpace(dto.CoverImageUrl))
                {
                    dto.CoverImageUrl = mediaResolver.Resolve(dto.CoverImageUrl, HttpContext, mode);
                }
            }

            if (includeTranslations)
            {
                await PopulateTranslationsAsync(new List<ContentDetailsDto> { dto });
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
    public async Task<ActionResult<string[]>> GetTags([FromQuery] string? language = null)
    {
        var query = dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(language))
        {
            query = query.Where(c => c.Language == language);
        }

        var tags = (await query.Select(c => c.Tags).ToArrayAsync()).SelectMany(z => z).Distinct().Where(str => !string.IsNullOrEmpty(str)).ToArray();
        return Ok(tags);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string[]>> GetCategories([FromQuery] string? language = null)
    {
        var query = dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(language))
        {
            query = query.Where(c => c.Language == language);
        }

        var categories = (await query.Select(c => c.Category).ToArrayAsync()).Distinct().Where(str => !string.IsNullOrEmpty(str)).ToArray();
        return Ok(categories);
    }

    [HttpGet("authors")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string[]>> GetAuthors([FromQuery] string? language = null)
    {
        var query = dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(language))
        {
            query = query.Where(c => c.Language == language);
        }

        var authors = (await query.Select(c => c.Author).ToArrayAsync()).Distinct().Where(str => !string.IsNullOrEmpty(str)).ToArray();
        return Ok(authors);
    }

    [HttpGet("categories/{contentType}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string[]>> GetCategoriesByContentType(string contentType, [FromQuery] string? language = null)
    {
        var query = dbSet.Where(c => c.Type == contentType);

        if (!string.IsNullOrEmpty(language))
        {
            query = query.Where(c => c.Language == language);
        }

        var categories = (await query.Select(c => c.Category).ToArrayAsync())
            .Distinct()
            .Where(str => !string.IsNullOrEmpty(str))
            .ToArray();

        return Ok(categories);
    }

    [HttpGet("authors/{contentType}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string[]>> GetAuthorsByContentType(string contentType, [FromQuery] string? language = null)
    {
        var query = dbSet.Where(c => c.Type == contentType);

        if (!string.IsNullOrEmpty(language))
        {
            query = query.Where(c => c.Language == language);
        }

        var authors = (await query.Select(c => c.Author).ToArrayAsync())
            .Distinct()
            .Where(str => !string.IsNullOrEmpty(str))
            .ToArray();

        return Ok(authors);
    }

    [HttpGet("tags/{contentType}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<string[]>> GetTagsByContentType(string contentType, [FromQuery] string? language = null)
    {
        var query = dbSet.Where(c => c.Type == contentType);

        if (!string.IsNullOrEmpty(language))
        {
            query = query.Where(c => c.Language == language);
        }

        var tags = (await query.Select(c => c.Tags).ToArrayAsync())
            .SelectMany(z => z)
            .Distinct()
            .Where(str => !string.IsNullOrEmpty(str))
            .ToArray();

        return Ok(tags);
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
    [IncludeBaseParameter(Description = "Include base versions of modified items for three-way merge support")]
    [ProducesResponseType(typeof(SyncResponseDto<ContentDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        var includeBase = HttpContext.Request.Query.ContainsKey("includeBase")
            && bool.TryParse(HttpContext.Request.Query["includeBase"], out var val)
            && val;

        return await syncService.SyncAsync<Content, ContentDetailsDto>(queryProviderFactory, mapper, syncToken, query, includeBase);
    }

    // PUT api/content/5
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult<ContentDetailsDto>> Put(int id, [FromBody] ContentCreateDto value)
    {
        var existingEntity = await FindOrThrowNotFound(id);

        // Create a new mapper configuration that maps all properties, including nulls
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<ContentCreateDto, Content>()
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => true)); // Always map, including nulls
        });
        var fullMapper = config.CreateMapper();

        // Map all properties from the DTO to the existing entity, including nulls
        fullMapper.Map(value, existingEntity);

        await dbContext.SaveChangesAsync();

        await OnAfterUpdateAsync(existingEntity);

        var resultDto = mapper.Map<ContentDetailsDto>(existingEntity);

        return Ok(resultDto);
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

        // Preserve the Type value from the original entity
        var originalEntity = await dbSet.FindAsync(id);
        if (originalEntity != null)
        {
            translationDraft.Type = originalEntity.Type;
        }

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

    /// <summary>
    /// Get change log records for a specific content item.
    /// Supports standard query parameters for filtering, sorting, and pagination like other endpoints.
    /// Query format: /api/content/{id}/change-log?filter[limit]=10&amp;filter[skip]=0&amp;filter[order]=CreatedAt&amp;filter[where][eq][EntityState]=Modified.
    /// </summary>
    /// <param name="id">The ID of the content item to get change logs for.</param>
    /// <param name="query">Query string with filter parameters (same format as other endpoints).</param>
    /// <returns>Paginated list of change log records with parsed content data.</returns>
    [HttpGet("{id}/change-log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ChangeLogDetailsDto<ContentUpdateDto>>>> GetChangeLog(int id, [FromQuery] string? query)
    {
        // Validate that the content exists using the standard pattern
        await FindOrThrowNotFound(id);

        // Parse query commands manually to create a custom query provider
        var queryCommands = QueryStringParser.Parse(Request.QueryString.HasValue ?
            System.Web.HttpUtility.UrlDecode(Request.QueryString.ToString()) : string.Empty);

        var queryBuilder = new QueryModelBuilder<ChangeLog>(
            queryCommands,
            apiSettingsConfig.Value.MaxListSize,
            dbContext);        // Create a pre-filtered base query for this specific content item
        var baseQuery = dbContext.Set<ChangeLog>().AsQueryable()
            .Where(cl => cl.ObjectType == "Content" && cl.ObjectId == id);

        // Create the DBQueryProvider with the pre-filtered base query
        var dbQueryProvider = new DBQueryProvider<ChangeLog>(baseQuery, queryBuilder);

        // Now get the result which should apply ordering, pagination correctly
        var queryResult = await dbQueryProvider.GetResult();
        var changeLogs = queryResult.Records ?? new List<ChangeLog>();
        var totalCount = queryResult.TotalCount;

        // Extract all user IDs and batch resolve display names
        var allUserIds = new List<string?>();
        var userIdMap = new Dictionary<int, (string? createdById, string? updatedById)>();

        foreach (var cl in changeLogs)
        {
            var createdById = changeLogService.ExtractCreatedById(cl.Data);
            var updatedById = changeLogService.ExtractUpdatedById(cl.Data);

            userIdMap[cl.Id] = (createdById, updatedById);
            allUserIds.Add(createdById);
            allUserIds.Add(updatedById);
        }

        // Batch resolve all user display names in a single operation
        var userDisplayNames = await changeLogService.BatchResolveUserDisplayNamesAsync(allUserIds);

        // Transform ChangeLog entities to DTOs with parsed data and extracted audit fields
        var changeLogDtos = changeLogs.Select(cl =>
        {
            var (createdById, updatedById) = userIdMap[cl.Id];

            return new ChangeLogDetailsDto<ContentUpdateDto>
            {
                Id = cl.Id,
                ObjectType = cl.ObjectType,
                ObjectId = cl.ObjectId,
                EntityState = cl.EntityState,
                Data = changeLogService.SafeParseData<ContentUpdateDto>(cl.Data),
                CreatedAt = cl.CreatedAt,
                Source = cl.Source,
                CreatedById = createdById,
                UpdatedById = updatedById,
                CreatedBy = createdById != null && userDisplayNames.TryGetValue(createdById, out var createdByName) ? createdByName : null,
                UpdatedBy = updatedById != null && userDisplayNames.TryGetValue(updatedById, out var updatedByName) ? updatedByName : null,
            };
        }).ToList();

        // Add pagination headers like other endpoints
        Response.Headers.Append(ResponseHeaderNames.TotalCount, totalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(changeLogDtos);
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
    /// Called after content is successfully created to clear MDX component cache if needed.
    /// </summary>
    protected override async Task OnAfterCreateAsync(Content entity)
    {
        await HandleContentSavedAsync(entity);
    }

    /// <summary>
    /// Called after content is successfully updated to clear MDX component cache if needed.
    /// </summary>
    protected override async Task OnAfterUpdateAsync(Content entity)
    {
        await HandleContentSavedAsync(entity);
    }

    /// <summary>
    /// Called after content is successfully deleted to clear MDX component cache if needed.
    /// </summary>
    protected override async Task OnAfterDeleteAsync(Content entity)
    {
        if (!string.IsNullOrEmpty(entity.Type))
        {
            await ClearCacheIfMdxType(entity.Type);
        }
    }

    /// <summary>
    /// Called after content is successfully imported to clear MDX component cache for affected content types.
    /// </summary>
    protected override async Task OnAfterImportAsync(List<Content> importedEntities, List<ContentImportDto> importRecords)
    {
        // Get unique content types from the import records and clear cache for MDX types
        var contentTypes = importRecords.Where(r => !string.IsNullOrEmpty(r.Type))
                                      .Select(r => r.Type!)
                                      .Distinct();

        foreach (var contentType in contentTypes)
        {
            await ClearCacheIfMdxType(contentType);
        }
    }

    private async Task HandleContentSavedAsync(Content entity)
    {
        if (!string.IsNullOrEmpty(entity.Type))
        {
            await ClearCacheIfMdxType(entity.Type);
        }

        await mediaUsageService.UpdateMediaDescriptionsFromContentAsync(entity.Body, entity.Type);
        await UpdateCoverImageMetadataAsync(entity);
    }

    private async Task UpdateCoverImageMetadataAsync(Content entity)
    {
        if (string.IsNullOrWhiteSpace(entity.CoverImageUrl))
        {
            return;
        }

        if (!TryParseMediaPath(entity.CoverImageUrl, out var scopeUid, out var fileName))
        {
            return;
        }

        var media = await dbContext.Media!
            .FirstOrDefaultAsync(m => m.ScopeUid == scopeUid &&
                                      (m.Name == fileName || m.OriginalName == fileName));

        if (media == null)
        {
            return;
        }

        var updated = false;

        if (string.IsNullOrWhiteSpace(media.Description) && !string.IsNullOrWhiteSpace(entity.Title))
        {
            media.Description = entity.Title;
            updated = true;
        }

        var tags = media.Tags ?? Array.Empty<string>();
        if (!Array.Exists(tags, tag => string.Equals(tag, "cover", StringComparison.OrdinalIgnoreCase)))
        {
            media.Tags = tags.Concat(new[] { "cover" })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            updated = true;
        }

        if (!updated)
        {
            return;
        }

        dbContext.Media!.Update(media);
        await dbContext.SaveChangesAsync();
    }

    private bool TryParseMediaPath(string url, out string scopeUid, out string fileName)
    {
        scopeUid = string.Empty;
        fileName = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var path = url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            path = absoluteUri.AbsolutePath;
        }

        var match = MediaPathRegex.Match(path);
        if (!match.Success)
        {
            return false;
        }

        var mediaPath = match.Value.Substring("/api/media/".Length);
        var parts = mediaPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        // ScopeUid can be nested (e.g., "folder/subfolder/scope"), so take all parts except the last one
        scopeUid = string.Join('/', parts.Take(parts.Length - 1));
        fileName = parts[^1];
        return true;
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

    private async Task<Dictionary<string, long>> GetContentTypeStatisticsWithQuery()
    {
        var statistics = new Dictionary<string, long>();

        // Get all content types from the database
        var contentTypes = await dbContext.ContentTypes!
            .Select(ct => ct.Uid)
            .ToListAsync();

        // Initialize all content types with 0 count
        foreach (var contentType in contentTypes)
        {
            statistics[contentType] = 0;
        }

        // Get individual counts for each content type
        foreach (var contentType in contentTypes)
        {
            var count = await GetCountForContentType(contentType);
            statistics[contentType] = count;
        }

        return statistics;
    }

    private async Task<long> GetCountForContentType(string contentType)
    {
        // Parse existing query commands
        var queryString = HttpContext.Request.QueryString.HasValue
            ? System.Web.HttpUtility.UrlDecode(HttpContext.Request.QueryString.ToString())
            : string.Empty;

        var queryCommands = string.IsNullOrEmpty(queryString)
            ? new List<QueryCommand>()
            : QueryStringParser.Parse(queryString).ToList();

        // Remove any existing type filters
        queryCommands.RemoveAll(cmd =>
            cmd.Type == FilterType.Where &&
            cmd.Props.Length > 0 &&
            cmd.Props[0].Equals("type", StringComparison.OrdinalIgnoreCase));

        // Add the specific content type filter
        queryCommands.Add(new QueryCommand
        {
            Type = FilterType.Where,
            Props = new[] { "type" },
            Value = contentType,
            Source = $"filter[where][type]={contentType}",
        });

        // Use a high limit to get total count, not paged results
        var queryBuilder = new QueryModelBuilder<Content>(queryCommands, int.MaxValue, dbContext);
        var dbSetQuery = dbContext.Set<Content>();
        var queryProvider = new DBQueryProvider<Content>(dbSetQuery!.AsQueryable<Content>(), queryBuilder);

        var result = await queryProvider.GetResult();
        return result.TotalCount;
    }

    /// <summary>
    /// Populates the translations property for a list of content DTOs.
    /// </summary>
    /// <param name="contentList">The list of content DTOs to populate translations for.</param>
    private async Task PopulateTranslationsAsync(List<ContentDetailsDto> contentList)
    {
        if (contentList == null || !contentList.Any())
        {
            return;
        }

        // Get supported languages from configuration
        var supportedLanguages = languageValidationService.GetSupportedLanguages();

        // Extract all translation keys from the content list (excluding nulls)
        var translationKeys = contentList
            .Where(c => !string.IsNullOrEmpty(c.TranslationKey))
            .Select(c => c.TranslationKey!)
            .Distinct()
            .ToList();

        // Get all translations for the translation keys (if any exist)
        var allTranslations = new List<dynamic>();
        if (translationKeys.Any())
        {
            allTranslations = (await dbSet
                .Where(c => translationKeys.Contains(c.TranslationKey!))
                .Select(c => new { c.Id, c.Language, c.TranslationKey })
                .ToListAsync()).Cast<dynamic>().ToList();
        }

        // Group translations by translation key (filter out null keys)
        var translationsByKey = allTranslations
            .Where(t => t.TranslationKey != null)
            .GroupBy(t => t.TranslationKey!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Populate translations for each content item
        foreach (var content in contentList)
        {
            var translations = new Dictionary<string, int?>();

            // Initialize all supported languages with null
            foreach (var language in supportedLanguages)
            {
                translations[language] = null;
            }

            // Set the content's own ID for its language
            if (!string.IsNullOrEmpty(content.Language))
            {
                translations[content.Language] = content.Id;
            }

            // If content has a translation key, populate with actual translation IDs from other languages
            if (!string.IsNullOrEmpty(content.TranslationKey) &&
                translationsByKey.TryGetValue(content.TranslationKey, out var contentTranslations))
            {
                foreach (var translation in contentTranslations)
                {
                    // Use the language code as key, overwriting the content's own language mapping if needed
                    if (translation.Language != null)
                    {
                        translations[translation.Language] = translation.Id;
                    }
                }
            }

            content.Translations = translations;
        }
    }
}