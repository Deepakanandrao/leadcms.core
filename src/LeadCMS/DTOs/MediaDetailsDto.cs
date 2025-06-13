// <copyright file="MediaDetailsDto.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.DTOs;

public class MediaDetailsDto
{
    public string Location { get; set; } = string.Empty;

    public int Id { get; set; }

    public string ScopeUid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long Size { get; set; } = 0;

    public string Extension { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}