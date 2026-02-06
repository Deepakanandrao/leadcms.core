// <copyright file="ContentGenerationController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LeadCMS.Core.AIAssistance.Controllers;

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
    public async Task<ActionResult<ContentCreateDto>> GenerateContent([FromBody] ContentGenerationRequest request)
    {
        var response = await contentGenerationService.GenerateContentAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Generate edits for existing content based on a prompt using AI.
    /// </summary>
    /// <param name="request">The content edit request containing the current content data and edit prompt.</param>
    /// <returns>Generated edits for the content based on user prompt.</returns>
    [HttpPost("ai-edit")]
    [SwaggerOperation(Tags = new[] { "Content" })]
    public async Task<ActionResult<ContentCreateDto>> EditContent([FromBody] ContentEditRequest request)
    {
        var response = await contentGenerationService.GenerateContentEditAsync(request);
        return Ok(response);
    }
}
