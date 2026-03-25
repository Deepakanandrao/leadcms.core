// <copyright file="BaseImportDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class BaseImportDtoWithIdAndSource : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [Optional]
    public int? Id { get; set; }

    [Optional]
    public string? Source { get; set; }
}

public class BaseImportDtoWithDates : BaseImportDtoWithIdAndSource
{
    [Optional]
    public DateTime? CreatedAt { get; set; }

    [Optional]
    public DateTime? UpdatedAt { get; set; }
}

public class BaseImportDto : BaseImportDtoWithDates
{
    [Optional]
    public string? CreatedByIp { get; set; }

    [Optional]
    public string? CreatedById { get; set; }

    [Optional]
    public string? CreatedByUserAgent { get; set; }

    [Optional]
    public string? UpdatedByIp { get; set; }

    [Optional]
    public string? UpdatedById { get; set; }

    [Optional]
    public string? UpdatedByUserAgent { get; set; }
}