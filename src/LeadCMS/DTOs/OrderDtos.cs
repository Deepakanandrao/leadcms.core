// <copyright file="OrderDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using LeadCMS.DataAnnotations;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;

namespace LeadCMS.DTOs;

public class OrderCreateDto
{
    [Required]
    public int ContactId { get; set; }

    [Required]
    public string RefNo { get; set; } = string.Empty;

    public string? OrderNumber { get; set; }

    public string? AffiliateName { get; set; }

    [Required]
    public decimal ExchangeRate { get; set; } = 1;

    [Required]
    public string Currency { get; set; } = string.Empty;

    public bool TestOrder { get; set; } = false;

    public string? Data { get; set; }

    [Optional]
    public string? Source { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public string[]? Tags { get; set; }

    public int? CampaignId { get; set; }
}

public class OrderUpdateDto : IPatchDto
{
    [Ignore]
    [JsonIgnore]
    public HashSet<string> NullProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public int? ContactId { get; set; }

    public string? RefNo { get; set; }

    public string? OrderNumber { get; set; }

    public string? AffiliateName { get; set; }

    public decimal? ExchangeRate { get; set; }

    public string? Currency { get; set; }

    public bool? TestOrder { get; set; }

    public string? Data { get; set; }

    public string? Source { get; set; }

    public OrderStatus? Status { get; set; }

    public string[]? Tags { get; set; }

    public int? CampaignId { get; set; }

    public decimal? Commission { get; set; }

    public decimal? Refund { get; set; }
}

public class OrderDetailsDto : OrderCreateDto
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int Quantity { get; set; }

    public decimal Total { get; set; }

    public decimal CurrencyTotal { get; set; }

    public decimal Commission { get; set; }

    public decimal Refund { get; set; }

    public new OrderStatus Status { get; set; }

    [Ignore]
    public List<OrderItemDetailsDto>? OrderItems { get; set; }

    [Ignore]
    public ContactDetailsDto? Contact { get; set; }
}

public class OrderImportDto : BaseImportDto
{
    [Optional]
    [SwaggerUnique]
    public string? RefNo { get; set; } = string.Empty;

    [Optional]
    public string? OrderNumber { get; set; }

    [Optional]
    public string? AffiliateName { get; set; }

    [Optional]
    public decimal? ExchangeRate { get; set; } = 1;

    [Required]
    public string? Currency { get; set; } = string.Empty;

    [Optional]
    public int? ContactId { get; set; }

    [Optional]
    [EmailAddress]
    [SurrogateForeignKey(typeof(Contact), "Email", "ContactId")]
    public string? ContactEmail { get; set; }

    [Optional]
    public bool? TestOrder { get; set; } = false;

    [Optional]
    public string? Data { get; set; }

    [Optional]
    public string[]? Tags { get; set; }

    [Optional]
    public OrderStatus? Status { get; set; }
}