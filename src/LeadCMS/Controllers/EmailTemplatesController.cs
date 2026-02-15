// <copyright file="EmailTemplatesController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class EmailTemplatesController : BaseController<EmailTemplate, EmailTemplateCreateDto, EmailTemplateUpdateDto, EmailTemplateDetailsDto>
{
    private readonly ITranslationService translationService;

    public EmailTemplatesController(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<EmailTemplate> queryProviderFactory, ITranslationService translationService, ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
        this.translationService = translationService;
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
    [ProducesResponseType(typeof(SyncResponseDto<EmailTemplateDetailsDto, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public override Task<IActionResult> Sync([FromQuery] string? syncToken = null, [FromQuery] string? query = null)
    {
        return base.Sync(syncToken, query);
    }
}