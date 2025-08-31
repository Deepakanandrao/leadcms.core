// <copyright file="TextGenerationController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace LeadCMS.Plugin.AI.Controllers;

[ApiController]
[Route("api/ai/[controller]")]
[Authorize]
public class TextGenerationController : ControllerBase
{
    private readonly ITextGenerationService textGenerationService;

    public TextGenerationController(ITextGenerationService textGenerationService)
    {
        this.textGenerationService = textGenerationService;
    }

    /// <summary>
    /// Generate text using AI models.
    /// </summary>
    /// <param name="request">Text generation request containing prompt and configuration.</param>
    /// <returns>Generated text response.</returns>
    [HttpPost]
    [SwaggerOperation(Tags = new[] { "AIAssistance" })]
    public async Task<ActionResult<TextGenerationResponse>> GenerateText([FromBody] TextGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserPrompt))
        {
            return BadRequest("User prompt is required");
        }

        try
        {
            var response = await textGenerationService.GenerateTextAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
