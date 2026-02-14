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

    public string? OriginalName { get; set; }

    public string? Description { get; set; }

    public long Size { get; set; } = 0;

    public long? OriginalSize { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public int? OriginalWidth { get; set; }

    public int? OriginalHeight { get; set; }

    public string Extension { get; set; } = string.Empty;

    public string? OriginalExtension { get; set; }

    public string MimeType { get; set; } = string.Empty;

    public string? OriginalMimeType { get; set; }

    public string[] Tags { get; set; } = Array.Empty<string>();

    public int UsageCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class MediaOptimizeResponseDto
{
    public int Updated { get; set; }

    public string? Message { get; set; }
}

/// <summary>
/// Represents a deleted or renamed-away media file path returned by the media sync API.
/// Clients use the ScopeUid and Name to identify which local file should be removed.
/// </summary>
public class MediaDeletedDto
{
    /// <summary>
    /// Gets or sets the folder path (scope) of the deleted/old media file.
    /// </summary>
    public string ScopeUid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name of the deleted/old media file.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for bulk media optimization with optional folder filtering.
/// </summary>
public class MediaBulkOptimizeRequestDto
{
    /// <summary>
    /// Gets or sets the optional folder path to limit optimization scope (e.g., "folder1" or "folder1/subfolder").
    /// When not set, all media files are processed.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include files in subfolders of the specified folder.
    /// Only applicable when Folder is set. Defaults to false.
    /// </summary>
    public bool IncludeSubfolders { get; set; } = false;
}

/// <summary>
/// Request DTO for bulk media reset with optional folder filtering.
/// </summary>
public class MediaBulkResetRequestDto
{
    /// <summary>
    /// Gets or sets the optional folder path to limit reset scope (e.g., "folder1" or "folder1/subfolder").
    /// When not set, all media files are processed.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to include files in subfolders of the specified folder.
    /// Only applicable when Folder is set. Defaults to false.
    /// </summary>
    public bool IncludeSubfolders { get; set; } = false;
}

/// <summary>
/// Request DTO for bulk media folder rename with optional subfolder handling.
/// </summary>
public class MediaBulkRenameRequestDto
{
    /// <summary>
    /// Gets or sets the source folder path to rename (e.g., "folder1" or "folder1/subfolder").
    /// </summary>
    [Required]
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the new folder path.
    /// </summary>
    [Required]
    public string NewFolder { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for bulk media folder delete.
/// </summary>
public class MediaBulkDeleteRequestDto
{
    /// <summary>
    /// Gets or sets the folder path to delete (e.g., "folder1" or "folder1/subfolder").
    /// All subfolders are included.
    /// </summary>
    [Required]
    public string Folder { get; set; } = string.Empty;
}

public class MediaRenameRequestDto
{
    [Required]
    public string ScopeUid { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string NewScopeUid { get; set; } = string.Empty;

    [Required]
    public string NewFileName { get; set; } = string.Empty;
}

public class MediaTransformRequestDto
{
    [Required]
    public string ScopeUid { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;
}

public class MediaResizeRequestDto : MediaTransformRequestDto
{
    [Range(1, int.MaxValue)]
    public int Width { get; set; }

    [Range(1, int.MaxValue)]
    public int Height { get; set; }

    public bool MaintainAspectRatio { get; set; } = true;
}

public class MediaCropRequestDto : MediaTransformRequestDto
{
    [Range(1, int.MaxValue)]
    public int Width { get; set; }

    [Range(1, int.MaxValue)]
    public int Height { get; set; }

    public int? X { get; set; }

    public int? Y { get; set; }
}