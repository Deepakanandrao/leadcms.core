// <copyright file="MediaController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
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
    public async Task<ActionResult> Post([FromForm] MediaCreateDto imageCreateDto)
    {
        var incomingFileName = imageCreateDto.File!.FileName.ToTranslit().Slugify();
        var incomingFileExtension = Path.GetExtension(imageCreateDto.File!.FileName);
        var incomingFileSize = imageCreateDto.File!.Length; // bytes
        var incomingFileMimeType = ContentTypeHelper.GetMimeTypeOrThrow(incomingFileName, ModelState);

        using var fileStream = imageCreateDto.File.OpenReadStream();
        var imageInBytes = new byte[incomingFileSize];
        fileStream.Read(imageInBytes, 0, (int)imageCreateDto.File.Length);

        var scopeAndFileExists = from i in pgDbContext!.Media!
                                 where i.ScopeUid == imageCreateDto.ScopeUid.Trim() && i.Name == incomingFileName
                                 select i;

        Media uploadedMedia;

        if (scopeAndFileExists.Any())
        {
            uploadedMedia = scopeAndFileExists!.FirstOrDefault()!;
            uploadedMedia!.Data = imageInBytes;
            uploadedMedia!.Size = incomingFileSize;

            // Update optional description if provided
            if (!string.IsNullOrWhiteSpace(imageCreateDto.Description))
            {
                uploadedMedia.Description = imageCreateDto.Description!.Trim();
            }

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
                Description = string.IsNullOrWhiteSpace(imageCreateDto.Description) ? null : imageCreateDto.Description!.Trim(),
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
            Description = uploadedMedia.Description,
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

        if (uploadedImageData == null)
        {
            throw new EntityNotFoundException(nameof(Media), pathToFile);
        }

        // Compute ETag (using file size and updatedAt/createdAt)
        DateTime lastModified = uploadedImageData.UpdatedAt ?? uploadedImageData.CreatedAt;
        string etag = $"\"{uploadedImageData.Size}-{lastModified.ToUniversalTime().Ticks}\"";
        string lastModifiedString = lastModified.ToUniversalTime().ToString("R"); // RFC1123

        // Set ETag and Last-Modified headers
        Response.Headers["ETag"] = etag;
        Response.Headers["Last-Modified"] = lastModifiedString;

        // Check If-None-Match and If-Modified-Since
        var ifNoneMatch = Request.Headers["If-None-Match"].FirstOrDefault();
        var ifModifiedSince = Request.Headers["If-Modified-Since"].FirstOrDefault();

        if ((!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag) ||
            (!string.IsNullOrEmpty(ifModifiedSince) &&
                DateTime.TryParse(ifModifiedSince, out var since) &&
                lastModified.ToUniversalTime() <= since.ToUniversalTime().AddSeconds(1)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        // For images, return FileStreamResult with no filename so Content-Disposition is not set
        if (uploadedImageData.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return new FileContentResult(uploadedImageData.Data, uploadedImageData.MimeType);
        }
        
        // For other types, keep default (attachment)
        return File(uploadedImageData.Data, uploadedImageData.MimeType, fname);
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
    public async Task<ActionResult<List<MediaDetailsDto>>> GetList(
        [FromQuery] string? query = null,
        [FromQuery] string? scopeUid = null,
        [FromQuery] bool includeFolders = false)
    {
        if (!includeFolders)
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
                Description = m.Description,
                Size = m.Size,
                Extension = m.Extension,
                MimeType = m.MimeType,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                Location = Path.Combine(HttpContext.Request.Path, m.ScopeUid, m.Name).Replace("\\", "/"),
            }).ToList();

            return Ok(mapped);
        }
        else
        {
            var scopePrefix = string.IsNullOrEmpty(scopeUid) ? string.Empty : scopeUid.TrimEnd('/');
            List<string> folderScopeUids;
            List<Media> files;

            if (string.IsNullOrEmpty(scopePrefix))
            {
                // Root: get all distinct first-level ScopeUid parts, filter out empty names and leading slashes
                var allScopeUids = await pgDbContext.Media!
                    .Where(m => !string.IsNullOrEmpty(m.ScopeUid))
                    .Select(m => m.ScopeUid)
                    .ToListAsync();

                folderScopeUids = allScopeUids
                    .Select(s => s.TrimStart('/'))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Split('/')[0])
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToList();

                // Files in root (ScopeUid is empty)
                files = await pgDbContext.Media!
                    .Where(m => string.IsNullOrEmpty(m.ScopeUid))
                    .Select(m => new Media
                    {
                        Id = m.Id,
                        ScopeUid = m.ScopeUid,
                        Name = m.Name,
                        Description = m.Description,
                        Size = m.Size,
                        Extension = m.Extension,
                        MimeType = m.MimeType,
                        CreatedAt = m.CreatedAt,
                        UpdatedAt = m.UpdatedAt,
                    })
                    .ToListAsync();
            }
            else
            {
                // Subfolder: get all distinct next-level ScopeUid parts, filter out empty names and leading slashes
                var prefix = scopePrefix + "/";

                var allScopeUids = await pgDbContext.Media!
                    .Where(m => m.ScopeUid.StartsWith(prefix))
                    .Select(m => m.ScopeUid.Substring(prefix.Length))
                    .ToListAsync();

                folderScopeUids = allScopeUids
                    .Select(s => s.TrimStart('/'))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s.Split('/')[0])
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .Select(s => scopePrefix + "/" + s)
                    .ToList();

                // Files in this folder
                files = await pgDbContext.Media!
                    .Where(m => m.ScopeUid == scopePrefix)
                    .Select(m => new Media
                    {
                        Id = m.Id,
                        ScopeUid = m.ScopeUid,
                        Name = m.Name,
                        Description = m.Description,
                        Size = m.Size,
                        Extension = m.Extension,
                        MimeType = m.MimeType,
                        CreatedAt = m.CreatedAt,
                        UpdatedAt = m.UpdatedAt,
                    })
                    .ToListAsync();
            }

            // Build folder DTOs
            var folderDtos = new List<MediaDetailsDto>();

            foreach (var folder in folderScopeUids.Distinct())
            {
                // For folder info, only query files in this folder and subfolders (excluding Data)
                var folderFiles = await pgDbContext.Media!
                    .Where(m => m.ScopeUid == folder || m.ScopeUid.StartsWith(folder + "/"))
                    .Select(m => new { m.Size, m.CreatedAt, m.UpdatedAt })
                    .ToListAsync();

                // Count subfolders in this folder
                var subfolderScopeUids = await pgDbContext.Media!
                    .Where(m => m.ScopeUid == folder || m.ScopeUid.StartsWith(folder + "/"))
                    .Select(m => m.ScopeUid)
                    .ToListAsync();
                var subfolderCount = subfolderScopeUids
                    .Where(s => s != folder && s.StartsWith(folder + "/"))
                    .Select(s => s.Substring(folder.Length + 1).Split('/')[0])
                    .Distinct()
                    .Count();
                var fileCount = folderFiles.Count;
                var totalCount = subfolderCount + fileCount;

                var createdAt = folderFiles.OrderBy(f => f.CreatedAt).FirstOrDefault()?.CreatedAt ?? DateTime.UtcNow;
                var updatedAt = folderFiles.OrderByDescending(f => f.UpdatedAt).FirstOrDefault()?.UpdatedAt;
                var size = folderFiles.Sum(f => f.Size);
                var namePart = folder.Split('/').Last();

                var humanName = Regex.Replace(namePart, "([a-z])([A-Z])", "$1 $2").Replace("-", " ").Replace("_", " ");
                humanName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(humanName);

                folderDtos.Add(new MediaDetailsDto
                {
                    Id = totalCount,
                    ScopeUid = folder,
                    Location = folder,
                    Name = humanName,
                    Description = null,
                    Size = size,
                    MimeType = "inode/directory",
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt,
                });
            }

            // File DTOs
            var fileDtos = files.Select(m => new MediaDetailsDto
            {
                Id = m.Id,
                ScopeUid = m.ScopeUid,
                Name = m.Name,
                Description = m.Description,
                Size = m.Size,
                Extension = m.Extension,
                MimeType = m.MimeType,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                Location = Path.Combine(HttpContext.Request.Path, m.ScopeUid, m.Name).Replace("\\", "/"),
            });

            var resultList = folderDtos.Concat(fileDtos).ToList();
            return Ok(resultList);
        }
    }

    [HttpPatch]
    [ProducesResponseType(typeof(MediaDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MediaDetailsDto>> Patch([FromForm] MediaUpdateDto mediaUpdateDto)
    {
        // Find existing media record by scope UID and file name
        var existingMedia = await pgDbContext.Media!.FirstOrDefaultAsync(m => 
            m.ScopeUid == mediaUpdateDto.ScopeUid.Trim() && m.Name == mediaUpdateDto.FileName.Trim());
        
        if (existingMedia == null)
        {
            throw new EntityNotFoundException(nameof(Media), $"{mediaUpdateDto.ScopeUid}/{mediaUpdateDto.FileName}");
        }

        // If a file is provided, update the binary keeping name/extension; otherwise only update metadata
        if (mediaUpdateDto.File != null)
        {
            // Validate that the new file has the same extension as the existing one
            var incomingFileExtension = Path.GetExtension(mediaUpdateDto.File.FileName);
            if (!string.Equals(existingMedia.Extension, incomingFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("File", $"File extension must match the existing media extension '{existingMedia.Extension}'.");
                throw new InvalidModelStateException(ModelState);
            }

            // Process the new file content
            var incomingFileSize = mediaUpdateDto.File.Length;
            var incomingFileMimeType = ContentTypeHelper.GetMimeTypeOrThrow(existingMedia.Name, ModelState);

            using var fileStream = mediaUpdateDto.File.OpenReadStream();
            var imageInBytes = new byte[incomingFileSize];
            await fileStream.ReadAsync(imageInBytes, 0, (int)mediaUpdateDto.File.Length);

            // Update only the binary content and size, preserve other properties (ScopeUid, Name, Extension)
            existingMedia.Data = imageInBytes;
            existingMedia.Size = incomingFileSize;
            existingMedia.MimeType = incomingFileMimeType;
        }

        // Update description if provided (can be set to empty to clear)
        if (mediaUpdateDto.Description != null)
        {
            var trimmed = mediaUpdateDto.Description.Trim();
            existingMedia.Description = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        pgDbContext.Media!.Update(existingMedia);
        await pgDbContext.SaveChangesAsync();

        Log.Information(
            "Updated media '{ScopeUid}/{FileName}' (ID: {Id}), preserving ScopeUid, Name, and Extension",
            existingMedia.ScopeUid,
            existingMedia.Name,
            existingMedia.Id);

        // Return the updated media details
        var updatedMediaDto = new MediaDetailsDto
        {
            Id = existingMedia.Id,
            ScopeUid = existingMedia.ScopeUid,
            Name = existingMedia.Name,
            Description = existingMedia.Description,
            Size = existingMedia.Size,
            Extension = existingMedia.Extension,
            MimeType = existingMedia.MimeType,
            CreatedAt = existingMedia.CreatedAt,
            UpdatedAt = existingMedia.UpdatedAt,
            Location = Path.Combine("/api/media", existingMedia.ScopeUid, existingMedia.Name).Replace("\\", "/"),
        };

        return Ok(updatedMediaDto);
    }
}