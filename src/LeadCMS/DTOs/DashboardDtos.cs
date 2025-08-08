// <copyright file="DashboardDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using LeadCMS.Geography;

namespace LeadCMS.DTOs;
// Common period query
public class PeriodQuery
{
    // Absolute range in UTC. If missing, 'Period' like 7d,30d,90d,1y will be used.
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? Period { get; set; } = "7d";

    public bool Compare { get; set; } = true;

    // Aggregation level for time-series endpoints
    public TimeGroupBy GroupBy { get; set; } = TimeGroupBy.Month;

    // Optional generic filters
    public Country? CountryCode { get; set; }

    public int? AccountId { get; set; }
}

public enum TimeGroupBy
{
    Day,
    Week,
    Month,
    Quarter,
    Year,
}

// CRM
public class CrmMetricsDto
{
    public long TotalContacts { get; set; }

    public double? ContactsChangePct { get; set; }

    public long TotalAccounts { get; set; }

    public double? AccountsChangePct { get; set; }

    public long TotalOrders { get; set; }

    public double? OrdersChangePct { get; set; }

    public decimal Revenue { get; set; }

    public double? RevenueChangePct { get; set; }
}

public class SalesPerformancePointDto
{
    // e.g. 2025-06 (year-month)
    [Required]
    public string Period { get; set; } = string.Empty;

    public decimal Revenue { get; set; }

    public int Orders { get; set; }
}

public class TopAccountDto
{
    public int AccountId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Revenue { get; set; }

    public double? ChangePct { get; set; }
}

public class OrderSummaryDto
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string Customer { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class ContactGrowthPointDto
{
    public string Period { get; set; } = string.Empty;

    public int Contacts { get; set; }
}

// CMS
public class ContentDistributionItemDto
{
    public string Name { get; set; } = string.Empty; // ContentType Uid or Category

    public int Value { get; set; }
}

public class TopContentItemDto
{
    public int ContentId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int CommentCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CommentSummaryDto
{
    public int Id { get; set; }

    public string User { get; set; } = string.Empty; // AuthorName

    public string Comment { get; set; } = string.Empty; // Body

    public DateTime CreatedAt { get; set; }

    public int? ArticleId { get; set; }

    public string? Article { get; set; }
}
