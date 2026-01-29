// <copyright file="CoverImageGenerationController.cs" company="WavePoint Co. Ltd.">
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
[Route("api/content/ai-cover")]
[Authorize]
public class CoverImageGenerationController : ControllerBase
{
    private readonly ICoverImageGenerationService coverImageGenerationService;

    public CoverImageGenerationController(ICoverImageGenerationService coverImageGenerationService)
    {
        this.coverImageGenerationService = coverImageGenerationService;
    }

    /// <summary>
    /// Generate a cover image for content using AI.
    /// </summary>
    /// <remarks>
    /// This endpoint generates a cover image based on the content title, description, and slug.
    /// The generated image is automatically saved to the media library.
    ///
    /// The AI uses recent cover images from your blog as style references. You can optionally:
    /// - Provide up to 5 specific sample image paths to use as style references
    /// - Include a custom prompt to guide the image generation
    ///
    /// The generated image will be saved at: /api/media/{slug}/cover.png.
    /// </remarks>
    /// <param name="request">The cover image generation request.</param>
    /// <returns>Information about the generated and saved cover image.</returns>
    [HttpPost]
    [SwaggerOperation(Tags = new[] { "Content" })]
    [ProducesResponseType(typeof(MediaDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MediaDetailsDto>> GenerateCoverImage([FromBody] CoverImageGenerationRequest request)
    {
        var response = await coverImageGenerationService.GenerateCoverImageAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Edit an existing cover image for content using AI.
    /// </summary>
    /// <remarks>
    /// This endpoint edits an existing cover image in the media library.
    /// The request identifies the image by URL (starting with /api/media/). The existing cover image
    /// is used as the primary image to edit, while optional sample images are used strictly
    /// as style references.
    /// </remarks>
    /// <param name="request">The cover image edit request.</param>
    /// <returns>Information about the updated cover image.</returns>
    [HttpPost("edit")]
    [SwaggerOperation(Tags = new[] { "Content" })]
    [ProducesResponseType(typeof(MediaDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MediaDetailsDto>> EditCoverImage([FromBody] CoverImageEditRequest request)
    {
        var response = await coverImageGenerationService.EditCoverImageAsync(request);
        return Ok(response);
    }
}
