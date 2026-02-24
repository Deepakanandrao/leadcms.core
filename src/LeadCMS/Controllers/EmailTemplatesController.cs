// <copyright file="EmailTemplatesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Attributes;
using LeadCMS.Configuration;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Exceptions;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class EmailTemplatesController : BaseController<EmailTemplate, EmailTemplateCreateDto, EmailTemplateUpdateDto, EmailTemplateDetailsDto>
{
    private readonly ITranslationService translationService;
    private readonly IChangeLogService changeLogService;
    private readonly IEmailGroupResolutionService emailGroupResolutionService;
    private readonly IOptions<ApiSettingsConfig> apiSettingsConfig;
    private readonly IEmailTemplateService emailTemplateService;

    public EmailTemplatesController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<EmailTemplate> queryProviderFactory, ITranslationService translationService, ISyncService syncService, IChangeLogService changeLogService, IEmailGroupResolutionService emailGroupResolutionService, IOptions<ApiSettingsConfig> apiSettingsConfig, IEmailTemplateService emailTemplateService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.translationService = translationService;
        this.changeLogService = changeLogService;
        this.emailGroupResolutionService = emailGroupResolutionService;
        this.apiSettingsConfig = apiSettingsConfig;
        this.emailTemplateService = emailTemplateService;
    }

    /// <inheritdoc/>
    public override async Task<ActionResult<EmailTemplateDetailsDto>> Post([FromBody] EmailTemplateCreateDto value)
    {
        await ThrowIfDuplicateNameLanguageAsync(value.Name, value.Language);
        return await base.Post(value);
    }

    /// <inheritdoc/>
    public override async Task<ActionResult<EmailTemplateDetailsDto>> Patch(int id, [FromBody] EmailTemplateUpdateDto value)
    {
        var existingEntity = await FindOrThrowNotFound(id);

        var effectiveName = value.Name ?? existingEntity.Name;
        var effectiveLanguage = value.Language ?? existingEntity.Language;

        // Only check for duplicates if name or language is actually changing
        if (value.Name != null || value.Language != null)
        {
            await ThrowIfDuplicateNameLanguageAsync(effectiveName, effectiveLanguage, id);
        }

        return await Patch(existingEntity, value);
    }

    /// <summary>
    /// Generates a rendered email template preview from inline template data.
    /// The template does not need to be saved — the caller supplies subject, body, format, and sender info.
    /// Renders the template using the specified contact's data, or generates a dummy contact
    /// with meaningful sample data when no contact ID is provided.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(EmailTemplatePreviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EmailTemplatePreviewResultDto>> Preview([FromBody] EmailTemplatePreviewRequestDto dto)
    {
        var result = await emailTemplateService.PreviewAsync(dto);
        return Ok(result);
    }

    /// <summary>
    /// Sends a test email using a contact's data but delivered to a specified email address.
    /// Does not require a saved campaign — useful for testing a template before creating one.
    /// </summary>
    [HttpPost("send-test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SendTest([FromBody] EmailTemplateSendTestDto dto)
    {
        await emailTemplateService.SendTestEmailAsync(dto);
        return Ok();
    }

    [HttpGet("{id}/translation-draft/{language}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmailTemplateDetailsDto>> GetTranslationDraft(int id, string language, [FromQuery] TranslationTransformerType transformer = TranslationTransformerType.EmptyCopy)
    {
        var translationDraft = await translationService.CreateTranslationDraftAsync<EmailTemplate>(id, language, transformer);

        // Resolve the target email group in the target language, or clear it if not found
        translationDraft.EmailGroupId = await emailGroupResolutionService.ResolveTargetEmailGroupIdAsync(translationDraft.EmailGroupId, language);

        var draftDto = mapper.Map<EmailTemplateDetailsDto>(translationDraft);

        return Ok(draftDto);
    }

    [HttpGet("{id}/translations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<EmailTemplateDetailsDto>>> GetTranslations(int id)
    {
        var translations = await translationService.GetTranslationsAsync<EmailTemplate>(id);
        return Ok(mapper.Map<List<EmailTemplateDetailsDto>>(translations));
    }

    /// <inheritdoc/>
    [HttpGet("sync")]
    [IncludeBaseParameter(Description = "Include base versions of modified items for three-way merge support")]
    [ProducesResponseType(typeof(SyncResponseDto<EmailTemplateDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override async Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        var includeBase = HttpContext.Request.Query.ContainsKey("includeBase")
            && bool.TryParse(HttpContext.Request.Query["includeBase"], out var val)
            && val;

        return await syncService.SyncAsync<EmailTemplate, EmailTemplateDetailsDto>(queryProviderFactory, mapper, syncToken, query, includeBase);
    }

    /// <summary>
    /// Get change log records for a specific email template.
    /// Supports standard query parameters for filtering, sorting, and pagination like other endpoints.
    /// Query format: /api/emailtemplates/{id}/change-log?filter[limit]=10&amp;filter[skip]=0&amp;filter[order]=CreatedAt&amp;filter[where][eq][EntityState]=Modified.
    /// </summary>
    /// <param name="id">The ID of the email template to get change logs for.</param>
    /// <param name="query">Query string with filter parameters (same format as other endpoints).</param>
    /// <returns>Paginated list of change log records with parsed email template data.</returns>
    [HttpGet("{id}/change-log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ChangeLogDetailsDto<EmailTemplateUpdateDto>>>> GetChangeLog(int id, [FromQuery] string? query)
    {
        await FindOrThrowNotFound(id);

        var queryCommands = QueryStringParser.Parse(Request.QueryString.HasValue ?
            System.Web.HttpUtility.UrlDecode(Request.QueryString.ToString()) : string.Empty);

        var queryBuilder = new QueryModelBuilder<ChangeLog>(
            queryCommands,
            apiSettingsConfig.Value.MaxListSize,
            dbContext);

        var baseQuery = dbContext.Set<ChangeLog>().AsQueryable()
            .Where(cl => cl.ObjectType == "EmailTemplate" && cl.ObjectId == id);

        var dbQueryProvider = new DBQueryProvider<ChangeLog>(baseQuery, queryBuilder);

        var queryResult = await dbQueryProvider.GetResult();
        var changeLogs = queryResult.Records ?? new List<ChangeLog>();
        var totalCount = queryResult.TotalCount;

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

        var userDisplayNames = await changeLogService.BatchResolveUserDisplayNamesAsync(allUserIds);

        var changeLogDtos = changeLogs.Select(cl =>
        {
            var (createdById, updatedById) = userIdMap[cl.Id];

            return new ChangeLogDetailsDto<EmailTemplateUpdateDto>
            {
                Id = cl.Id,
                ObjectType = cl.ObjectType,
                ObjectId = cl.ObjectId,
                EntityState = cl.EntityState,
                Data = changeLogService.SafeParseData<EmailTemplateUpdateDto>(cl.Data),
                CreatedAt = cl.CreatedAt,
                Source = cl.Source,
                CreatedById = createdById,
                UpdatedById = updatedById,
                CreatedBy = createdById != null && userDisplayNames.TryGetValue(createdById, out var createdByName) ? createdByName : null,
                UpdatedBy = updatedById != null && userDisplayNames.TryGetValue(updatedById, out var updatedByName) ? updatedByName : null,
            };
        }).ToList();

        Response.Headers.Append(ResponseHeaderNames.TotalCount, totalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        return Ok(changeLogDtos);
    }

    private async Task ThrowIfDuplicateNameLanguageAsync(string name, string language, int? excludeId = null)
    {
        var duplicateExists = await dbContext.EmailTemplates!
            .AnyAsync(et => et.Name == name && et.Language == language && (!excludeId.HasValue || et.Id != excludeId.Value));

        if (duplicateExists)
        {
            throw new ConflictException($"An email template with name '{name}' already exists for language '{language}'.");
        }
    }
}