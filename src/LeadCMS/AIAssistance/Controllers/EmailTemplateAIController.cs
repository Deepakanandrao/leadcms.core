// <copyright file="EmailTemplateAIController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.DTOs;
using LeadCMS.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LeadCMS.Core.AIAssistance.Controllers;

[ApiController]
[Route("api/email-templates")]
[Authorize(Roles = "Admin")]
public class EmailTemplateAIController : ControllerBase
{
    private readonly IEmailTemplateAITranslationService emailTemplateAITranslationService;
    private readonly IEmailTemplateGenerationService emailTemplateGenerationService;

    public EmailTemplateAIController(
        IEmailTemplateAITranslationService emailTemplateAITranslationService,
        IEmailTemplateGenerationService emailTemplateGenerationService)
    {
        this.emailTemplateAITranslationService = emailTemplateAITranslationService;
        this.emailTemplateGenerationService = emailTemplateGenerationService;
    }

    /// <summary>
    /// Generate an AI-powered translation draft for email template.
    /// </summary>
    /// <param name="id">The ID of the email template to translate.</param>
    /// <param name="language">The target language for the translation.</param>
    /// <param name="emailGroupId">Optional email group ID to place the translated template in a different group.</param>
    /// <returns>The AI-translated email template draft.</returns>
    [HttpGet("{id}/ai-translation-draft/{language}")]
    [SwaggerOperation(Tags = new[] { "EmailTemplates" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmailTemplateDetailsDto>> GetAITranslationDraft(int id, string language, [FromQuery] int? emailGroupId = null)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new BadRequestException("Language parameter is required");
        }

        var translationDraft = await emailTemplateAITranslationService.CreateAITranslationDraftAsync(id, language, emailGroupId);
        return Ok(translationDraft);
    }

    /// <summary>
    /// Generate new email template from a prompt using AI.
    /// </summary>
    /// <param name="request">The email template generation request containing language, email group, and prompt.</param>
    /// <returns>Generated email template based on existing samples and user prompt.</returns>
    [HttpPost("ai-draft")]
    [SwaggerOperation(Tags = new[] { "EmailTemplates" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmailTemplateDetailsDto>> GenerateEmailTemplate([FromBody] EmailTemplateGenerationRequest request)
    {
        var response = await emailTemplateGenerationService.GenerateEmailTemplateAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Generate edits for existing email template based on a prompt using AI.
    /// </summary>
    /// <param name="request">The email template edit request containing the current template data and edit prompt.</param>
    /// <returns>Generated edits for the email template based on user prompt.</returns>
    [HttpPost("ai-edit")]
    [SwaggerOperation(Tags = new[] { "EmailTemplates" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EmailTemplateDetailsDto>> EditEmailTemplate([FromBody] EmailTemplateEditRequest request)
    {
        var response = await emailTemplateGenerationService.GenerateEmailTemplateEditAsync(request);
        return Ok(response);
    }
}