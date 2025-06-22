// <copyright file="BulkResponseBodyDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json.Serialization;

namespace LeadCMS.Elastic;

public class BulkResponseBodyDto
{
    [JsonPropertyName("errors")]
    public bool Errors { get; set; }

    [JsonPropertyName("items")]
    public List<BulkItemDto>? Items { get; set; }
}

public class BulkItemDto
{
    [JsonPropertyName("index")]
    public BulkItemDetailDto? Index { get; set; }
}

public class BulkItemDetailDto
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("error")]
    public BulkErrorDto? Error { get; set; }
}

public class BulkErrorDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
