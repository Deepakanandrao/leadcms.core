// <copyright file="MediaDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.DataAnnotations;

namespace LeadCMS.DTOs;

public class MediaCreateDto
{
    [Required]
    [MediaExtension]
    public IFormFile? File { get; set; }

    [Required]
    public string ScopeUid { get; set; } = string.Empty;

    // Optional description for media
    public string? Description { get; set; }

    // Optional tags for media
    public string[]? Tags { get; set; }
}

public class MediaUpdateDto
{
    // Optional new file content; if omitted, only metadata (e.g., Description) can be updated
    [MediaExtension]
    public IFormFile? File { get; set; }

    [Required]
    public string ScopeUid { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;

    // Optional description update
    public string? Description { get; set; }

    // Optional tags update
    public string[]? Tags { get; set; }
}

public class MediaDetailsDto
{
    public string Location { get; set; } = string.Empty;

    public int Id { get; set; }

    public string ScopeUid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long Size { get; set; } = 0;

    public string Extension { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public string[] Tags { get; set; } = Array.Empty<string>();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}