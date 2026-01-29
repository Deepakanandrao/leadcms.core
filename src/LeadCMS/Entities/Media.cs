// <copyright file="Media.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LeadCMS.DataAnnotations;

namespace LeadCMS.Entities
{
    [Table("media")]
    public class Media : BaseEntity
    {
        [Required]
        [Searchable]
        public string ScopeUid { get; set; } = string.Empty;

        [Searchable]
        public string Name { get; set; } = string.Empty;

        [Searchable]
        public string? OriginalName { get; set; }

        [Searchable]
        public string? Description { get; set; }

        public long Size { get; set; } = 0;

        public long? OriginalSize { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public int? OriginalWidth { get; set; }

        public int? OriginalHeight { get; set; }

        [Searchable]
        public string Extension { get; set; } = string.Empty;

        [Searchable]
        public string? OriginalExtension { get; set; }

        [Searchable]
        public string MimeType { get; set; } = string.Empty;

        [Searchable]
        public string? OriginalMimeType { get; set; }

        [Searchable]
        public string[] Tags { get; set; } = Array.Empty<string>();

        public int UsageCount { get; set; } = 0;

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public byte[]? OriginalData { get; set; }
    }
}