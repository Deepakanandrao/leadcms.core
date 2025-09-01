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
        var response = await imageGenerationService.GenerateImageAsync(request);
        return Ok(response);
    }
}
