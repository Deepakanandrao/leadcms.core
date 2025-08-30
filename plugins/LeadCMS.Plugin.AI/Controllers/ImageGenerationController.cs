// <copyright file="ImageGenerationController.cs" company="WavePoint Co. Ltd.">
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
public class ImageGenerationController : ControllerBase
{
    private readonly IImageGenerationService imageGenerationService;

    public ImageGenerationController(IImageGenerationService imageGenerationService)
    {
        this.imageGenerationService = imageGenerationService;
    }

    /// <summary>
    /// Generate images using AI models.
    /// </summary>
    /// <param name="request">Image generation request containing prompt and configuration.</param>
    /// <returns>Generated image response.</returns>
    [HttpPost]
    [SwaggerOperation(Tags = new[] { "AIAssistance" })]
    public async Task<ActionResult<ImageGenerationResponse>> GenerateImage([FromBody] ImageGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required");
        }

        try
        {
            var response = await imageGenerationService.GenerateImageAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate image with a simple prompt.
    /// </summary>
    /// <param name="prompt">The image prompt.</param>
    /// <param name="size">Optional image size (default: 1024x1024).</param>
    /// <param name="quality">Optional image quality (standard/hd, default: standard).</param>
    /// <returns>Generated image response.</returns>
    [HttpPost("simple")]
    [SwaggerOperation(Tags = new[] { "AIAssistance" })]
    public async Task<ActionResult<ImageGenerationResponse>> GenerateSimpleImage(
        [FromBody] string prompt,
        [FromQuery] string size = "1024x1024",
        [FromQuery] string quality = "standard")
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return BadRequest("Prompt is required");
        }

        var request = new ImageGenerationRequest
        {
            Prompt = prompt,
            Size = size,
            Quality = quality,
        };

        try
        {
            var response = await imageGenerationService.GenerateImageAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
