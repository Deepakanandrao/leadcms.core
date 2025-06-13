// <copyright file="MediaController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

[Authorize]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly PgDbContext pgDbContext;
    private readonly QueryProviderFactory<Media> queryProviderFactory;

    public MediaController(PgDbContext pgDbContext, QueryProviderFactory<Media> queryProviderFactory)
    {
        this.pgDbContext = pgDbContext;
        this.queryProviderFactory = queryProviderFactory;
    }

    [HttpPost]
    [ProducesResponseType(typeof(MediaDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Post([FromForm] ImageCreateDto imageCreateDto)
    {
        var incomingFileName = imageCreateDto.Image!.FileName.ToTranslit().Slugify();
        var incomingFileExtension = Path.GetExtension(imageCreateDto.Image!.FileName);
        var incomingFileSize = imageCreateDto.Image!.Length; // bytes
        var incomingFileMimeType = ContentTypeHelper.GetMimeTypeOrThrow(incomingFileName, ModelState);

        using var fileStream = imageCreateDto.Image.OpenReadStream();
        var imageInBytes = new byte[incomingFileSize];
        fileStream.Read(imageInBytes, 0, (int)imageCreateDto.Image.Length);

        var scopeAndFileExists = from i in pgDbContext!.Media!
                                 where i.ScopeUid == imageCreateDto.ScopeUid.Trim() && i.Name == incomingFileName
                                 select i;

        Media uploadedMedia;

        if (scopeAndFileExists.Any())
        {
            uploadedMedia = scopeAndFileExists!.FirstOrDefault()!;
            uploadedMedia!.Data = imageInBytes;
            uploadedMedia!.Size = incomingFileSize;
            pgDbContext.Media!.Update(uploadedMedia);
        }
        else
        {
            uploadedMedia = new Media()
            {
                Name = incomingFileName,
                Size = incomingFileSize,
                Data = imageInBytes,
                MimeType = incomingFileMimeType!,
                ScopeUid = imageCreateDto.ScopeUid.Trim(),
                Extension = incomingFileExtension,
            };
            await pgDbContext.Media!.AddAsync(uploadedMedia);
        }

        await pgDbContext.SaveChangesAsync();

        Log.Information("Request scheme {0}", HttpContext.Request.Scheme);
        Log.Information("Request host {0}", HttpContext.Request.Host.Value);

        var fileData = new MediaDetailsDto()
        {
            Id = uploadedMedia.Id,
            ScopeUid = uploadedMedia.ScopeUid,
            Name = uploadedMedia.Name,
            Size = uploadedMedia.Size,
            Extension = uploadedMedia.Extension,
            MimeType = uploadedMedia.MimeType,
            CreatedAt = uploadedMedia.CreatedAt,
            UpdatedAt = uploadedMedia.UpdatedAt,
            Location = Path.Combine(HttpContext.Request.Path, uploadedMedia.ScopeUid, uploadedMedia.Name).Replace("\\", "/"),
        };

        return CreatedAtAction(nameof(Get), new { scopeUid = uploadedMedia.ScopeUid, fileName = uploadedMedia.Name }, fileData);
    }

    [HttpGet]
    [AllowAnonymous]
    [Route("{*pathToFile}")]
    [ResponseCache(CacheProfileName = "ImageResponse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Get([Required] string pathToFile)
    {
        pathToFile = Uri.UnescapeDataString(pathToFile);

        var scope = Path.GetDirectoryName(pathToFile)!.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fname = Path.GetFileName(pathToFile);

        var uploadedImageData = await pgDbContext!.Media!.FirstOrDefaultAsync(e => e.ScopeUid == scope && e.Name == fname);

        return uploadedImageData == null
            ? throw new EntityNotFoundException(nameof(Media), pathToFile)
            : (ActionResult)File(uploadedImageData!.Data, uploadedImageData.MimeType, fname);
    }

    [HttpDelete]
    [Route("{*pathToFile}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Delete([Required] string pathToFile)
    {
        pathToFile = Uri.UnescapeDataString(pathToFile);

        var scope = Path.GetDirectoryName(pathToFile)!.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fname = Path.GetFileName(pathToFile);

        var mediaToDelete = await pgDbContext!.Media!.FirstOrDefaultAsync(e => e.ScopeUid == scope && e.Name == fname);

        if (mediaToDelete == null)
        {
            throw new EntityNotFoundException(nameof(Media), pathToFile);
        }

        pgDbContext.Media!.Remove(mediaToDelete);
        await pgDbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<MediaDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<MediaDetailsDto>>> GetList([FromQuery] string? query = null)
    {
        var qp = queryProviderFactory.BuildQueryProvider();
        var result = await qp.GetResult();

        Response.Headers.Append(ResponseHeaderNames.TotalCount, result.TotalCount.ToString());
        Response.Headers.Append(ResponseHeaderNames.AccessControlExposeHeader, ResponseHeaderNames.TotalCount);

        var mediaList = result.Records ?? new List<Media>();

        var mapped = mediaList.Select(m => new MediaDetailsDto
        {
            Id = m.Id,
            ScopeUid = m.ScopeUid,
            Name = m.Name,
            Size = m.Size,
            Extension = m.Extension,
            MimeType = m.MimeType,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
            Location = Path.Combine(HttpContext.Request.Path, m.ScopeUid, m.Name).Replace("\\", "/"),
        }).ToList();

        return Ok(mapped);
    }
}