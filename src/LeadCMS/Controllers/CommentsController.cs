// <copyright file="CommentsController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
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
public class CommentsController : BaseControllerWithImport<Comment, CommentCreateDto, CommentUpdateDto, CommentDetailsDto, CommentImportDto>
{
    private static readonly Dictionary<string, Type> CommentableTypes = FindCommentableTypes();

    private readonly ICommentService commentService;
    private readonly CommentableControllerExtension commentableControllerExtension;
    private readonly ITranslationService translationService;

    public CommentsController(PgDbContext dbContext, IMapper mapper, ICommentService commentService, EsDbContext esDbContext, QueryProviderFactory<Comment> queryProviderFactory, CommentableControllerExtension commentableControllerExtension, ITranslationService translationService, ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.commentService = commentService;
        this.commentableControllerExtension = commentableControllerExtension;
        this.translationService = translationService;
        additionalImportChecker = new CommentsImportChecker(dbContext);
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<List<CommentDetailsDto>>> Get([FromQuery] string? query)
    {
        var returnedItems = (await base.Get(query)).Result;

        var items = (List<CommentDetailsDto>)((ObjectResult)returnedItems!).Value!;

        return commentableControllerExtension.ReturnComments(items, this);
    }

    [HttpGet("with-statistics")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CommentsWithStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CommentsWithStatisticsDto>> GetWithStatistics([FromQuery] string? query)
    {
        // Get the comments using the base method (returns CommentDetailsDto from database)
        var returnedItems = (await base.Get(query)).Result;
        var items = (List<CommentDetailsDto>)((ObjectResult)returnedItems!).Value!;

        // Get statistics for all comment statuses and answer statuses using query without status filter
        var allStatistics = await GetCommentStatisticsWithQuery();

        // Process items through commentable extension
        var processedResult = commentableControllerExtension.ReturnComments(items, this);

        // Check if the user is authenticated to return appropriate DTO
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var processedItems = (List<CommentDetailsDto>)((ObjectResult)processedResult.Result!).Value!;
            var result = new CommentsWithStatisticsDto
            {
                Comments = processedItems,
                Statistics = allStatistics,
            };
            return Ok(result);
        }
        else
        {
            var processedItems = (List<AnonymousCommentDetailsDto>)((ObjectResult)processedResult.Result!).Value!;
            var result = new AnonymousCommentsWithStatisticsDto
            {
                Comments = processedItems,
                Statistics = allStatistics,
            };
            return Ok(result);
        }
    }

    // GET api/{entity}s/5
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<CommentDetailsDto>> GetOne(int id)
    {
        var result = (await base.GetOne(id)).Result;

        var commentDetails = (CommentDetailsDto)((ObjectResult)result!).Value!;

        commentDetails!.AvatarUrl = GravatarHelper.EmailToGravatarUrl(commentDetails.AuthorEmail);

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return Ok(commentDetails!);
        }
        else
        {
            var commentForAnonymous = mapper.Map<AnonymousCommentDetailsDto>(commentDetails);

            return Ok(commentForAnonymous!);
        }
    }

    // POST api/{entity}s
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<ActionResult<CommentDetailsDto>> Post([FromBody] CommentCreateDto value)
    {
        var commentableUid = string.Empty;
        ICommentable? commentable = null;

        if (CommentableTypes.TryGetValue(value.CommentableType, out var type))
        {
            var commentableDbSet = dbContext.SetDbEntity(type);

            if (value.CommentableId != null)
            {
                commentableUid = value.CommentableId!.ToString();
                commentable = (await commentableDbSet.FirstOrDefaultAsync(c => ((BaseEntityWithId)c).Id == value.CommentableId.Value)) as ICommentable;
            }
            else if (!string.IsNullOrEmpty(value.CommentableUid) && type == typeof(Content))
            {
                commentableUid = value.CommentableUid;
                commentable = (await commentableDbSet.FirstOrDefaultAsync(c => ((Content)c).Slug == value.CommentableUid)) as ICommentable;
            }
        }

        if (string.IsNullOrEmpty(commentableUid))
        {
            ModelState.AddModelError("CommentableId", "CommentableId or CommentableUid is required");
            throw new InvalidModelStateException(ModelState);
        }

        if (commentable == null)
        {
            throw new EntityNotFoundException(value.CommentableType, commentableUid);
        }

        value.CommentableId = ((BaseEntityWithId)commentable).Id;

        var comment = mapper.Map<Comment>(value);

        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            comment.Status = CommentStatus.Answer;
        }
        else
        {
            comment.Status = CommentStatus.NotApproved;
        }

        return await commentableControllerExtension.PostComment(comment, this);
    }

    [HttpGet("{id}/translation-draft/{language}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CommentDetailsDto>> GetTranslationDraft(int id, string language, [FromQuery] TranslationTransformerType transformer = TranslationTransformerType.EmptyCopy)
    {
        var translationDraft = await translationService.CreateTranslationDraftAsync<Comment>(id, language, transformer);
        var draftDto = mapper.Map<CommentDetailsDto>(translationDraft);

        return Ok(draftDto);
    }

    [HttpGet("{id}/translations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<CommentDetailsDto>>> GetTranslations(int id)
    {
        var translations = await translationService.GetTranslationsAsync<Comment>(id);
        return Ok(mapper.Map<List<CommentDetailsDto>>(translations));
    }

    [HttpGet("sync")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SyncResponseDto<CommentDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        // Get the sync result from the base implementation
        var baseResult = await base.Sync(syncToken, query);

        // Check if the request is authenticated
        bool isAuthenticated = User.Identity?.IsAuthenticated == true;

        if (isAuthenticated)
        {
            // Return the full result for authenticated users
            return baseResult;
        }
        else
        {
            // Transform the result for anonymous users
            if (baseResult is OkObjectResult okResult && okResult.Value is SyncResponseDto<CommentDetailsDto, int> syncResponse)
            {
                // Map to anonymous DTOs
                var anonymousItems = syncResponse.Items.Select(item => mapper.Map<AnonymousCommentDetailsDto>(item)).ToList();

                var anonymousResponse = new SyncResponseDto<AnonymousCommentDetailsDto, int>
                {
                    Items = anonymousItems,
                    Deleted = syncResponse.Deleted,
                };

                return Ok(anonymousResponse);
            }

            // Return the original result if transformation is not possible
            return baseResult;
        }
    }

    protected override async Task SaveRangeAsync(List<Comment> comments)
    {
        await commentService.SaveRangeAsync(comments);
    }

    private static Dictionary<string, Type> FindCommentableTypes()
    {
        var assembly = Assembly.GetAssembly(typeof(ICommentable));
        return assembly!.GetTypes().Where(t => t.IsClass && typeof(ICommentable).IsAssignableFrom(t)).ToDictionary(t => ICommentable.GetCommentableType(t), t => t);
    }

    private async Task<Dictionary<string, long>> GetCommentStatisticsWithQuery()
    {
        var statistics = new Dictionary<string, long>();

        // Initialize all comment statuses with 0 count
        foreach (CommentStatus status in Enum.GetValues<CommentStatus>())
        {
            statistics[status.ToString()] = 0;
        }

        // Initialize all answer statuses with 0 count
        foreach (AnswerStatus answerStatus in Enum.GetValues<AnswerStatus>())
        {
            statistics[answerStatus.ToString()] = 0;
        }

        // Get individual counts for each CommentStatus
        foreach (CommentStatus status in Enum.GetValues<CommentStatus>())
        {
            var count = await GetCountForCommentStatus(status);
            statistics[status.ToString()] = count;
        }

        // Get individual counts for each AnswerStatus
        foreach (AnswerStatus answerStatus in Enum.GetValues<AnswerStatus>())
        {
            var count = await GetCountForAnswerStatus(answerStatus);
            statistics[answerStatus.ToString()] = count;
        }

        return statistics;
    }

    private async Task<long> GetCountForCommentStatus(CommentStatus status)
    {
        // Parse existing query commands
        var queryString = HttpContext.Request.QueryString.HasValue
            ? System.Web.HttpUtility.UrlDecode(HttpContext.Request.QueryString.ToString())
            : string.Empty;

        var queryCommands = string.IsNullOrEmpty(queryString)
            ? new List<QueryCommand>()
            : QueryStringParser.Parse(queryString).ToList();

        // Remove any existing status filters
        queryCommands.RemoveAll(cmd =>
            cmd.Type == FilterType.Where &&
            cmd.Props.Length > 0 &&
            cmd.Props[0].Equals("status", StringComparison.OrdinalIgnoreCase));

        // Remove any existing answer status filters to avoid conflicts
        queryCommands.RemoveAll(cmd =>
            cmd.Type == FilterType.Where &&
            cmd.Props.Length > 0 &&
            cmd.Props[0].Equals("answerStatus", StringComparison.OrdinalIgnoreCase));

        // Add the specific status filter
        queryCommands.Add(new QueryCommand
        {
            Type = FilterType.Where,
            Props = new[] { "status" },
            Value = status.ToString(),
            Source = $"filter[where][status]={status}",
        });

        // Use a high limit to get total count, not paged results
        var queryBuilder = new QueryModelBuilder<Comment>(queryCommands, int.MaxValue, dbContext);
        var dbSet = dbContext.Set<Comment>();
        var queryProvider = new DBQueryProvider<Comment>(dbSet!.AsQueryable<Comment>(), queryBuilder);

        var result = await queryProvider.GetResult();
        return result.TotalCount;
    }

    private async Task<long> GetCountForAnswerStatus(AnswerStatus answerStatus)
    {
        // Parse existing query commands
        var queryString = HttpContext.Request.QueryString.HasValue
            ? System.Web.HttpUtility.UrlDecode(HttpContext.Request.QueryString.ToString())
            : string.Empty;

        var queryCommands = string.IsNullOrEmpty(queryString)
            ? new List<QueryCommand>()
            : QueryStringParser.Parse(queryString).ToList();

        // Remove any existing comment status filters to avoid conflicts
        queryCommands.RemoveAll(cmd =>
            cmd.Type == FilterType.Where &&
            cmd.Props.Length > 0 &&
            cmd.Props[0].Equals("status", StringComparison.OrdinalIgnoreCase));

        // Remove any existing answer status filters
        queryCommands.RemoveAll(cmd =>
            cmd.Type == FilterType.Where &&
            cmd.Props.Length > 0 &&
            cmd.Props[0].Equals("answerStatus", StringComparison.OrdinalIgnoreCase));

        // Add the specific answer status filter
        queryCommands.Add(new QueryCommand
        {
            Type = FilterType.Where,
            Props = new[] { "answerStatus" },
            Value = answerStatus.ToString(),
            Source = $"filter[where][answerStatus]={answerStatus}",
        });

        // Use a high limit to get total count, not paged results
        var queryBuilder = new QueryModelBuilder<Comment>(queryCommands, int.MaxValue, dbContext);
        var dbSet = dbContext.Set<Comment>();
        var queryProvider = new DBQueryProvider<Comment>(dbSet!.AsQueryable<Comment>(), queryBuilder);

        var result = await queryProvider.GetResult();
        return result.TotalCount;
    }

    private sealed class CommentsImportChecker : AdditionalImportChecker
    {
        private readonly PgDbContext dbContext;
        private readonly Dictionary<string, List<int>> existedCommentableIds = new Dictionary<string, List<int>>();
        private List<CommentImportDto> importRecords = new List<CommentImportDto>();

        public CommentsImportChecker(PgDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public override void SetData(List<CommentImportDto> importRecords)
        {
            this.importRecords = importRecords;
            foreach (var commentableType in CommentableTypes)
            {
                var importRecordIds = importRecords
                    .Where(r => r.CommentableType == commentableType.Key && r.CommentableId.HasValue)
                    .Select(ir => ir.CommentableId!.Value)
                    .ToHashSet();
                var existedIds = dbContext.SetDbEntity(commentableType.Value)
                    .Where(c => importRecordIds.Contains(((BaseEntityWithId)c).Id))
                    .Select(c => ((BaseEntityWithId)c).Id)
                    .ToList();
                existedCommentableIds.Add(commentableType.Key, existedIds);
            }
        }

        public override bool Check(int index, ImportResult result)
        {
            if (index < 0 || index > importRecords.Count)
            {
                return false;
            }

            var importRecord = importRecords[index];

            // For new records (id = 0), CommentableType and CommentableId must be set
            if (importRecord.Id.GetValueOrDefault(0) == 0)
            {
                if (string.IsNullOrEmpty(importRecord.CommentableType))
                {
                    result.AddError(index, "CommentableType is required when creating new comments");
                    return false;
                }

                if (!importRecord.CommentableId.HasValue || importRecord.CommentableId.Value == 0)
                {
                    result.AddError(index, "CommentableId is required when creating new comments");
                    return false;
                }

                // Check if the referenced commentable entity exists
                if (!existedCommentableIds.ContainsKey(importRecord.CommentableType) ||
                    !existedCommentableIds[importRecord.CommentableType].Contains(importRecord.CommentableId.Value))
                {
                    result.AddError(index, $"Commentable entity of type {importRecord.CommentableType} with id = {importRecord.CommentableId} cannot be found");
                    return false;
                }
            }

            return true;
        }
    }
}