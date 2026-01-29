// <copyright file="ContentTranslationController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.DTOs;
using LeadCMS.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LeadCMS.Core.AIAssistance.Controllers;

[ApiController]
[Route("api/content")]
[Authorize(Roles = "Admin")]
public class ContentTranslationController : ControllerBase
{
    private readonly IContentAITranslationService contentAITranslationService;

    public ContentTranslationController(IContentAITranslationService contentAITranslationService)
    {
        this.contentAITranslationService = contentAITranslationService;
    }

    /// <summary>
    /// Generate an AI-powered translation draft for content.
    /// </summary>
    /// <param name="id">The ID of the content to translate.</param>
    /// <param name="language">The target language for the translation.</param>
    /// <returns>The AI-translated content draft.</returns>
    [HttpGet("{id}/ai-translation-draft/{language}")]
    [SwaggerOperation(Tags = new[] { "Content" })]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ContentDetailsDto>> GetAITranslationDraft(int id, string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new BadRequestException("Language parameter is required");
        }

        var translationDraft = await contentAITranslationService.CreateAITranslationDraftAsync(id, language);
        return Ok(translationDraft);
    }
}
