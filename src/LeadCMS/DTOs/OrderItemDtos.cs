// <copyright file="OrderItemDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class OrderItemCreateDto
{
    [Required]
    public int OrderId { get; set; }

    public int? LineNumber { get; set; }

    [Required]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    public decimal UnitPrice { get; set; } = 0;

    [CurrencyCode]
    [Required]
    public string Currency { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Minimum quantity should be 1")]
    public int Quantity { get; set; } = 0;

    [Optional]
    public string? Source { get; set; }
}

public class OrderItemUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [MinLength(1)]
    public string? ProductName { get; set; }

    public decimal? UnitPrice { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Minimum quantity should be 1")]
    public int? Quantity { get; set; }

    public string? Data { get; set; }
}

public class OrderItemDetailsDto : OrderItemCreateDto
{
    public int Id { get; set; }

    public new int LineNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public decimal CurrencyTotal { get; set; } = 0;

    public decimal Total { get; set; } = 0;

    [Ignore]
    public OrderDetailsDto? Order { get; set; }
}

public class OrderItemImportDto : BaseImportDto
{
    [Optional]
    public int? OrderId { get; set; }

    [Optional]
    [SurrogateForeignKey(typeof(Order), "RefNo", "OrderId")]
    public string? OrderRefNo { get; set; } = string.Empty;

    [Optional]
    public int? LineNumber { get; set; }

    [Optional]
    public string? ProductName { get; set; } = string.Empty;

    [Optional]
    public decimal? UnitPrice { get; set; } = 0;

    [Optional]
    [CurrencyCode]
    public string? Currency { get; set; }

    [Optional]
    public int? Quantity { get; set; }
}