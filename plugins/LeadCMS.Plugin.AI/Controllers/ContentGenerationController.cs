// <copyright file="ContentGenerationController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LeadCMS.Plugin.AI.Controllers;

[ApiController]
[Route("api/content")]
[Authorize]
public class ContentGenerationController : ControllerBase
{
    private readonly IContentGenerationService contentGenerationService;

    public ContentGenerationController(IContentGenerationService contentGenerationService)
    {
        this.contentGenerationService = contentGenerationService;
    }

    /// <summary>
    /// Generate new content of a specific type from a prompt using AI.
    /// </summary>
    /// <param name="request">The content generation request containing language, content type, and prompt.</param>
    /// <returns>Generated content based on existing samples and user prompt.</returns>
    [HttpPost("ai-draft")]
    [SwaggerOperation(Tags = new[] { "Content" })]
    public async Task<ActionResult<ContentDetailsDto>> GenerateContent([FromBody] ContentGenerationRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required");
        }

        if (string.IsNullOrWhiteSpace(request.Language))
        {
            return BadRequest("Language is required");
        }

        if (string.IsNullOrWhiteSpace(request.ContentType))
        {
            return BadRequest("ContentType is required");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required");
        }

        try
        {
            var response = await contentGenerationService.GenerateContentAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex) when (ex.Message.Contains("Not enough data in the database"))
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
