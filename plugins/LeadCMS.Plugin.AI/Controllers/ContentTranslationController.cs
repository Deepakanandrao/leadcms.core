// <copyright file="ContentTranslationController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LeadCMS.Plugin.AI.Controllers;

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
            return BadRequest("Language parameter is required");
        }

        try
        {
            var translationDraft = await contentAITranslationService.CreateAITranslationDraftAsync(id, language);
            return Ok(translationDraft);
        }
        catch (EntityNotFoundException)
        {
            return NotFound();
        }
        catch (TranslationConflictException)
        {
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Translation already exists",
                Detail = $"A translation for language '{language}' already exists for this content.",
            });
        }
        catch (UnsupportedLanguageException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Unsupported language",
                Detail = ex.Message,
                Extensions = { ["language"] = language },
            });
        }
        catch (NotTranslatableException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Content not translatable",
                Detail = "This content type does not support translations.",
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ProblemDetails
            {
                Status = 500,
                Title = "AI translation failed",
                Detail = "Failed to generate AI translation. Please try again later.",
            });
        }
    }
}
