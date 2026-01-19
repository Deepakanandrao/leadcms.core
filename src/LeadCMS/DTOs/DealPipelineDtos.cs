// <copyright file="DealPipelineDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class DealPipelineCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
}

public class DealPipelineUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [Required]
    public string Name { get; set; } = string.Empty;
}

public class DealPipelineDetailsDto : DealPipelineCreateDto
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [Ignore]
    public List<DealPipelineStageDetailsDto>? PipelineStages { get; set; }
}