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

    /// <summary>
    /// Gets or sets the number of contacts that have at least one paid order in the period.
    /// </summary>
    public long PaidContacts { get; set; }

    public double? PaidContactsChangePct { get; set; }

    /// <summary>
    /// Gets or sets the number of accounts that have at least one contact with a paid order in the period.
    /// </summary>
    public long PaidAccounts { get; set; }

    public double? PaidAccountsChangePct { get; set; }

    public long TotalOrders { get; set; }

    public double? OrdersChangePct { get; set; }

    /// <summary>
    /// Gets or sets revenue — sum of Order.Total (vendor payout amount, excl. tax/discounts/commissions) for non-test orders.
    /// </summary>
    public decimal Revenue { get; set; }

    public double? RevenueChangePct { get; set; }

    /// <summary>
    /// Gets or sets total refund amount in the period.
    /// </summary>
    public decimal TotalRefunds { get; set; }

    /// <summary>
    /// Gets or sets total affiliate / platform commissions in the period.
    /// </summary>
    public decimal TotalCommissions { get; set; }
}

public class SalesPerformancePointDto
{
    // e.g. 2025-06 (year-month)
    [Required]
    public string Period { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets revenue — sum of Order.Total.
    /// </summary>
    public decimal Revenue { get; set; }

    /// <summary>
    /// Gets or sets total refunds in the period.
    /// </summary>
    public decimal Refunds { get; set; }

    /// <summary>
    /// Gets or sets total commissions (affiliate / platform fees).
    /// </summary>
    public decimal Commissions { get; set; }

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
public class CmsMetricsDto
{
    public long TotalContent { get; set; }

    public double? ContentChangePct { get; set; }

    public long ContentUpdates { get; set; }

    public double? ContentUpdatesChangePct { get; set; }

    public long TotalMedia { get; set; }

    public double? MediaChangePct { get; set; }

    public long TotalComments { get; set; }

    public double? CommentsChangePct { get; set; }
}

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

public class ContentSummaryDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }
}

public class ContentGrowthPointDto
{
    public string Period { get; set; } = string.Empty;

    public int Contents { get; set; }
}

public class TopAuthorDto
{
    public string Author { get; set; } = string.Empty;

    public int Count { get; set; }

    public double? ChangePct { get; set; }
}
