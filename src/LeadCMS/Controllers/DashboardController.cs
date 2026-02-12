// <copyright file="DashboardController.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Globalization;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly PgDbContext db;

        public DashboardController(PgDbContext dbContext, IMapper mapper)
        {
            db = dbContext;
        }

        // CRM: key metrics
        [HttpGet("crm/metrics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CrmMetricsDto>> GetCrmMetrics([FromQuery] PeriodQuery query)
        {
            var (start, end, prev) = ResolveRange(query);

            // Total contacts created in range
            var contactsQ = ApplyContactFilters(db.Contacts!.AsNoTracking(), query)
                .Where(c => c.CreatedAt >= start && c.CreatedAt <= end);
            var contactsCount = await contactsQ.LongCountAsync();

            // Accounts created in range
            var accountsQ = db.Accounts!.AsNoTracking().Where(a => a.CreatedAt >= start && a.CreatedAt <= end);
            var accountsCount = await accountsQ.LongCountAsync();

            // Orders in range (exclude tests)
            var ordersQ = db.Orders!.AsNoTracking()
                .Where(o => !o.TestOrder && o.CreatedAt >= start && o.CreatedAt <= end);
            var ordersCount = await ordersQ.LongCountAsync();

            // Revenue & auxiliary totals
            var revenue = (decimal)(await ordersQ.SumAsync(o => (double?)o.Total) ?? 0d);
            var totalRefunds = (decimal)(await ordersQ.SumAsync(o => (double?)o.Refund) ?? 0d);
            var totalCommissions = (decimal)(await ordersQ.SumAsync(o => (double?)o.Commission) ?? 0d);

            // Paid contacts — distinct contacts created in range with at least one paid order in range
            var paidOrdersQ = ordersQ.Where(o => o.Status == OrderStatus.Paid);
            var paidContactsCount = await (
                from o in paidOrdersQ
                join c in contactsQ on o.ContactId equals c.Id
                select o.ContactId
            ).Distinct().LongCountAsync();

            // Paid accounts — distinct accounts linked to contacts created in range with paid orders in range
            var paidAccountsCount = await (
                from o in paidOrdersQ
                join c in contactsQ on o.ContactId equals c.Id
                where c.AccountId != null
                select c.AccountId!.Value
            ).Distinct().LongCountAsync();

            double? contactsChange = null, accountsChange = null, ordersChange = null;
            double? revenueChange = null;
            double? paidContactsChange = null, paidAccountsChange = null;
            if (prev.HasValue)
            {
                var p = prev.Value;
                var pContactsQ = ApplyContactFilters(db.Contacts!.AsNoTracking(), query)
                    .Where(c => c.CreatedAt >= p.from && c.CreatedAt <= p.to);
                var pContactsCount = await pContactsQ.LongCountAsync();
                var pAccountsQ = db.Accounts!.AsNoTracking().Where(a => a.CreatedAt >= p.from && a.CreatedAt <= p.to);
                var pAccountsCount = await pAccountsQ.LongCountAsync();
                var pOrdersQ = db.Orders!.AsNoTracking().Where(o => !o.TestOrder && o.CreatedAt >= p.from && o.CreatedAt <= p.to);
                var pOrdersCount = await pOrdersQ.LongCountAsync();
                var pRevenue = (decimal)(await pOrdersQ.SumAsync(o => (double?)o.Total) ?? 0d);

                var pPaidOrdersQ = pOrdersQ.Where(o => o.Status == OrderStatus.Paid);
                var pPaidContactsCount = await (
                    from o in pPaidOrdersQ
                    join c in pContactsQ on o.ContactId equals c.Id
                    select o.ContactId
                ).Distinct().LongCountAsync();
                var pPaidAccountsCount = await (
                    from o in pPaidOrdersQ
                    join c in pContactsQ on o.ContactId equals c.Id
                    where c.AccountId != null
                    select c.AccountId!.Value
                ).Distinct().LongCountAsync();

                contactsChange = pContactsCount == 0 ? null : (contactsCount - pContactsCount) * 100.0 / pContactsCount;
                accountsChange = pAccountsCount == 0 ? null : (accountsCount - pAccountsCount) * 100.0 / pAccountsCount;
                ordersChange = pOrdersCount == 0 ? null : (ordersCount - pOrdersCount) * 100.0 / pOrdersCount;
                revenueChange = pRevenue == 0 ? null : (double)((revenue - pRevenue) * 100m / pRevenue);
                paidContactsChange = pPaidContactsCount == 0 ? null : (paidContactsCount - pPaidContactsCount) * 100.0 / pPaidContactsCount;
                paidAccountsChange = pPaidAccountsCount == 0 ? null : (paidAccountsCount - pPaidAccountsCount) * 100.0 / pPaidAccountsCount;
            }

            var dto = new CrmMetricsDto
            {
                TotalContacts = contactsCount,
                ContactsChangePct = contactsChange,
                TotalAccounts = accountsCount,
                AccountsChangePct = accountsChange,
                PaidContacts = paidContactsCount,
                PaidContactsChangePct = paidContactsChange,
                PaidAccounts = paidAccountsCount,
                PaidAccountsChangePct = paidAccountsChange,
                TotalOrders = ordersCount,
                OrdersChangePct = ordersChange,
                Revenue = revenue,
                RevenueChangePct = revenueChange,
                TotalRefunds = totalRefunds,
                TotalCommissions = totalCommissions,
            };

            return Ok(dto);
        }

        // CMS: key metrics
        [HttpGet("cms/metrics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<CmsMetricsDto>> GetCmsMetrics([FromQuery] PeriodQuery query)
        {
            var (start, end, prev) = ResolveRange(query);

            // New content created in range
            var contentQ = db.Content!.AsNoTracking().Where(c => c.CreatedAt >= start && c.CreatedAt <= end);
            var totalContent = await contentQ.LongCountAsync();

            // Content updates based on ChangeLog (ObjectType == nameof(Content) and EntityState == Modified)
            var updatesQ = db.ChangeLogs!.AsNoTracking()
                .Where(cl => cl.ObjectType == nameof(Content) && cl.EntityState == EntityState.Modified && cl.CreatedAt >= start && cl.CreatedAt <= end);
            var contentUpdates = await updatesQ.LongCountAsync();

            // Media created in range
            var mediaQ = db.Media!.AsNoTracking().Where(m => m.CreatedAt >= start && m.CreatedAt <= end);
            var totalMedia = await mediaQ.LongCountAsync();

            // Comments created in range
            var commentsQ = db.Comments!.AsNoTracking().Where(c => c.CreatedAt >= start && c.CreatedAt <= end);
            var totalComments = await commentsQ.LongCountAsync();

            double? contentChange = null, updatesChange = null, mediaChange = null, commentsChange = null;
            if (prev.HasValue)
            {
                var p = prev.Value;
                var pContent = await db.Content!.AsNoTracking().Where(c => c.CreatedAt >= p.from && c.CreatedAt <= p.to).LongCountAsync();
                var pUpdates = await db.ChangeLogs!.AsNoTracking()
                    .Where(cl => cl.ObjectType == nameof(Content) && cl.EntityState == EntityState.Modified && cl.CreatedAt >= p.from && cl.CreatedAt <= p.to)
                    .LongCountAsync();
                var pMedia = await db.Media!.AsNoTracking().Where(m => m.CreatedAt >= p.from && m.CreatedAt <= p.to).LongCountAsync();
                var pComments = await db.Comments!.AsNoTracking().Where(c => c.CreatedAt >= p.from && c.CreatedAt <= p.to).LongCountAsync();

                contentChange = pContent == 0 ? null : (totalContent - pContent) * 100.0 / pContent;
                updatesChange = pUpdates == 0 ? null : (contentUpdates - pUpdates) * 100.0 / pUpdates;
                mediaChange = pMedia == 0 ? null : (totalMedia - pMedia) * 100.0 / pMedia;
                commentsChange = pComments == 0 ? null : (totalComments - pComments) * 100.0 / pComments;
            }

            var dto = new CmsMetricsDto
            {
                TotalContent = totalContent,
                ContentChangePct = contentChange,
                ContentUpdates = contentUpdates,
                ContentUpdatesChangePct = updatesChange,
                TotalMedia = totalMedia,
                MediaChangePct = mediaChange,
                TotalComments = totalComments,
                CommentsChangePct = commentsChange,
            };

            return Ok(dto);
        }

        // CRM: sales performance aggregated by month within range
        [HttpGet("crm/sales-performance")]
        public async Task<ActionResult<List<SalesPerformancePointDto>>> GetSalesPerformance([FromQuery] PeriodQuery query)
        {
            var (start, end, _) = ResolveRange(query);
            var baseQ = db.Orders!.AsNoTracking()
                .Where(o => !o.TestOrder && o.CreatedAt >= start && o.CreatedAt <= end);

            List<SalesPerformancePointDto> items;
            switch (query.GroupBy)
            {
                case TimeGroupBy.Day:
                    {
                        var raw = await baseQ
                            .GroupBy(o => o.CreatedAt.Date)
                            .Select(g => new
                            {
                                PeriodStart = g.Key,
                                Revenue = g.Sum(x => (double)x.Total),
                                Refunds = g.Sum(x => (double)x.Refund),
                                Commissions = g.Sum(x => (double)x.Commission),
                                Orders = g.Count(),
                            })
                            .OrderBy(x => x.PeriodStart)
                            .ToListAsync();
                        items = raw.Select(x => MapSalesPoint(FormatPeriodLabel(x.PeriodStart, TimeGroupBy.Day), x.Revenue, x.Refunds, x.Commissions, x.Orders)).ToList();
                        break;
                    }

                case TimeGroupBy.Month:
                    {
                        var raw = await baseQ
                            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                            .Select(g => new
                            {
                                g.Key.Year,
                                g.Key.Month,
                                Revenue = g.Sum(x => (double)x.Total),
                                Refunds = g.Sum(x => (double)x.Refund),
                                Commissions = g.Sum(x => (double)x.Commission),
                                Orders = g.Count(),
                            })
                            .OrderBy(x => x.Year).ThenBy(x => x.Month)
                            .ToListAsync();
                        items = raw.Select(x => MapSalesPoint(FormatPeriodLabel(new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc), TimeGroupBy.Month), x.Revenue, x.Refunds, x.Commissions, x.Orders)).ToList();
                        break;
                    }

                case TimeGroupBy.Year:
                    {
                        var raw = await baseQ
                            .GroupBy(o => o.CreatedAt.Year)
                            .Select(g => new
                            {
                                Year = g.Key,
                                Revenue = g.Sum(x => (double)x.Total),
                                Refunds = g.Sum(x => (double)x.Refund),
                                Commissions = g.Sum(x => (double)x.Commission),
                                Orders = g.Count(),
                            })
                            .OrderBy(x => x.Year)
                            .ToListAsync();
                        items = raw.Select(x => MapSalesPoint(x.Year.ToString("D4", CultureInfo.InvariantCulture), x.Revenue, x.Refunds, x.Commissions, x.Orders)).ToList();
                        break;
                    }

                case TimeGroupBy.Week:
                case TimeGroupBy.Quarter:
                default:
                    {
                        var raw = await baseQ
                            .Select(o => new { o.CreatedAt, o.Total, o.Refund, o.Commission })
                            .ToListAsync();
                        var aggregated = raw
                            .GroupBy(x => query.GroupBy == TimeGroupBy.Week
                                ? GetIsoWeekStartUtc(x.CreatedAt)
                                : GetQuarterStartUtc(x.CreatedAt))
                            .Select(g => new
                            {
                                PeriodStart = g.Key,
                                Revenue = g.Sum(x => (double)x.Total),
                                Refunds = g.Sum(x => (double)x.Refund),
                                Commissions = g.Sum(x => (double)x.Commission),
                                Orders = g.Count(),
                            })
                            .OrderBy(x => x.PeriodStart)
                            .ToList();
                        items = aggregated.Select(x => MapSalesPoint(FormatPeriodLabel(x.PeriodStart, query.GroupBy), x.Revenue, x.Refunds, x.Commissions, x.Orders)).ToList();
                        break;
                    }
            }

            return Ok(items);
        }

        // CRM: top accounts by revenue (sum of orders of contacts mapped to account)
        [HttpGet("crm/top-accounts")]
        public async Task<ActionResult<List<TopAccountDto>>> GetTopAccounts([FromQuery] PeriodQuery query, [FromQuery] int limit = 5)
        {
            var (start, end, prev) = ResolveRange(query);

            var orders = db.Orders!.AsNoTracking().Where(o => !o.TestOrder && o.CreatedAt >= start && o.CreatedAt <= end);

            var q = from o in orders
                    join c in db.Contacts!.AsNoTracking() on o.ContactId equals c.Id
                    where c.AccountId != null
                    join a in db.Accounts!.AsNoTracking() on c.AccountId equals a.Id
                    group new { o, a } by new { a.Id, a.Name } into g
                    select new TopAccountDto
                    {
                        AccountId = g.Key.Id,
                        Name = g.Key.Name,
                        Revenue = (decimal)g.Sum(x => (double)x.o.Total),
                    };

            var list = await q.OrderByDescending(x => x.Revenue).Take(limit).ToListAsync();

            if (prev.HasValue && list.Count > 0)
            {
                var p = prev.Value;
                var prevOrders = db.Orders!.AsNoTracking().Where(o => !o.TestOrder && o.CreatedAt >= p.from && o.CreatedAt <= p.to);
                var prevQ = from o in prevOrders
                            join c in db.Contacts!.AsNoTracking() on o.ContactId equals c.Id
                            where c.AccountId != null
                            join a in db.Accounts!.AsNoTracking() on c.AccountId equals a.Id
                            group new { o, a } by a.Id into g
                            select new { AccountId = g.Key, Revenue = (decimal)g.Sum(x => (double)x.o.Total) };
                var prevDict = await prevQ.ToDictionaryAsync(x => x.AccountId, x => x.Revenue);

                foreach (var item in list)
                {
                    if (prevDict.TryGetValue(item.AccountId, out var pRev) && pRev != 0)
                    {
                        item.ChangePct = (double)((item.Revenue - pRev) * 100m / pRev);
                    }
                }
            }

            return Ok(list);
        }

        // CRM: recent orders
        [HttpGet("crm/recent-orders")]
        public async Task<ActionResult<List<OrderSummaryDto>>> GetRecentOrders([FromQuery] int limit = 5)
        {
            var q = from o in db.Orders!.AsNoTracking().Where(o => !o.TestOrder).OrderByDescending(o => o.CreatedAt)
                    join c in db.Contacts!.AsNoTracking() on o.ContactId equals c.Id
                    select new OrderSummaryDto
                    {
                        Id = o.Id,
                        OrderNumber = o.OrderNumber ?? string.Empty,
                        Customer = (c.FirstName ?? string.Empty) + (string.IsNullOrEmpty(c.LastName) ? string.Empty : " " + c.LastName),
                        Amount = o.Total,
                        Status = o.Status.ToString(),
                        CreatedAt = o.CreatedAt,
                    };

            var list = await q.Take(limit).ToListAsync();
            return Ok(list);
        }

        // CRM: contact growth (created per month)
        [HttpGet("crm/contact-growth")]
        public async Task<ActionResult<List<ContactGrowthPointDto>>> GetContactGrowth([FromQuery] PeriodQuery query)
        {
            var (start, end, _) = ResolveRange(query);
            var contacts = ApplyContactFilters(db.Contacts!.AsNoTracking(), query)
                .Where(c => c.CreatedAt >= start && c.CreatedAt <= end);

            List<ContactGrowthPointDto> list;
            switch (query.GroupBy)
            {
                case TimeGroupBy.Day:
                    {
                        var raw = await contacts
                            .GroupBy(c => c.CreatedAt.Date)
                            .Select(g => new { PeriodStart = g.Key, Contacts = g.Count() })
                            .OrderBy(x => x.PeriodStart)
                            .ToListAsync();
                        list = raw.Select(x => new ContactGrowthPointDto
                        {
                            Period = FormatPeriodLabel(x.PeriodStart, TimeGroupBy.Day),
                            Contacts = x.Contacts,
                        }).ToList();
                        break;
                    }

                case TimeGroupBy.Month:
                    {
                        var raw = await contacts
                            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                            .Select(g => new { g.Key.Year, g.Key.Month, Contacts = g.Count() })
                            .OrderBy(x => x.Year).ThenBy(x => x.Month)
                            .ToListAsync();
                        list = raw.Select(x => new ContactGrowthPointDto
                        {
                            Period = FormatPeriodLabel(new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc), TimeGroupBy.Month),
                            Contacts = x.Contacts,
                        }).ToList();
                        break;
                    }

                case TimeGroupBy.Year:
                    {
                        var raw = await contacts
                            .GroupBy(c => c.CreatedAt.Year)
                            .Select(g => new { Year = g.Key, Contacts = g.Count() })
                            .OrderBy(x => x.Year)
                            .ToListAsync();
                        list = raw.Select(x => new ContactGrowthPointDto
                        {
                            Period = x.Year.ToString("D4", CultureInfo.InvariantCulture),
                            Contacts = x.Contacts,
                        }).ToList();
                        break;
                    }

                case TimeGroupBy.Week:
                case TimeGroupBy.Quarter:
                default:
                    {
                        var raw = await contacts
                            .Select(c => new { c.CreatedAt })
                            .ToListAsync();
                        var aggregated = raw
                            .GroupBy(x => query.GroupBy == TimeGroupBy.Week
                                ? GetIsoWeekStartUtc(x.CreatedAt)
                                : GetQuarterStartUtc(x.CreatedAt))
                            .Select(g => new { PeriodStart = g.Key, Contacts = g.Count() })
                            .OrderBy(x => x.PeriodStart)
                            .ToList();
                        list = aggregated.Select(x => new ContactGrowthPointDto
                        {
                            Period = FormatPeriodLabel(x.PeriodStart, query.GroupBy),
                            Contacts = x.Contacts,
                        }).ToList();
                        break;
                    }
            }

            return Ok(list);
        }

        // CMS: top content by comment count (proxy for engagement)
        [HttpGet("cms/top-content")]
        public async Task<ActionResult<List<TopContentItemDto>>> GetTopContent([FromQuery] PeriodQuery query, [FromQuery] int limit = 5)
        {
            var (start, end, _) = ResolveRange(query);
            var q = from c in db.Content!.AsNoTracking()
                    join cm in db.Comments!.AsNoTracking() on c.Id equals cm.CommentableId into gj
                    from sub in gj.DefaultIfEmpty()
                    where c.CreatedAt >= start && c.CreatedAt <= end && (sub == null || sub.CommentableType == "Content")
                    group sub by new { c.Id, c.Title, c.CreatedAt } into g
                    select new TopContentItemDto
                    {
                        ContentId = g.Key.Id,
                        Title = g.Key.Title,
                        CommentCount = g.Count(x => x != null),
                        CreatedAt = g.Key.CreatedAt,
                    };

            var list = await q.OrderByDescending(x => x.CommentCount).ThenByDescending(x => x.CreatedAt).Take(limit).ToListAsync();
            return Ok(list);
        }

        // CMS: content distribution by type
        [HttpGet("cms/content-distribution")]
        public async Task<ActionResult<List<ContentDistributionItemDto>>> GetContentDistribution([FromQuery] PeriodQuery query)
        {
            var (start, end, _) = ResolveRange(query);
            var list = await db.Content!.AsNoTracking()
                .Where(c => c.CreatedAt >= start && c.CreatedAt <= end)
                .GroupBy(c => c.Type)
                .Select(g => new ContentDistributionItemDto { Name = g.Key, Value = g.Count() })
                .OrderByDescending(x => x.Value)
                .ToListAsync();
            return Ok(list);
        }

        // CMS: recent content
        [HttpGet("cms/recent-content")]
        public async Task<ActionResult<List<ContentSummaryDto>>> GetRecentContent([FromQuery] int limit = 5)
        {
            var list = await db.Content!.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .Select(c => new ContentSummaryDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Type = c.Type,
                    Author = c.Author,
                    CreatedAt = c.CreatedAt,
                    PublishedAt = c.PublishedAt,
                })
                .ToListAsync();
            return Ok(list);
        }

        // CMS: content growth (created per period)
        [HttpGet("cms/content-growth")]
        public async Task<ActionResult<List<ContentGrowthPointDto>>> GetContentGrowth([FromQuery] PeriodQuery query)
        {
            var (start, end, _) = ResolveRange(query);
            var baseQ = db.Content!.AsNoTracking().Where(c => c.CreatedAt >= start && c.CreatedAt <= end);

            List<ContentGrowthPointDto> items;
            switch (query.GroupBy)
            {
                case TimeGroupBy.Day:
                    {
                        var raw = await baseQ
                            .GroupBy(c => c.CreatedAt.Date)
                            .Select(g => new { PeriodStart = g.Key, Count = g.Count() })
                            .OrderBy(x => x.PeriodStart)
                            .ToListAsync();
                        items = raw.Select(x => new ContentGrowthPointDto
                        {
                            Period = FormatPeriodLabel(x.PeriodStart, TimeGroupBy.Day),
                            Contents = x.Count,
                        }).ToList();
                        break;
                    }

                case TimeGroupBy.Month:
                    {
                        var raw = await baseQ
                            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                            .OrderBy(x => x.Year).ThenBy(x => x.Month)
                            .ToListAsync();
                        items = raw.Select(x => new ContentGrowthPointDto
                        {
                            Period = FormatPeriodLabel(new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc), TimeGroupBy.Month),
                            Contents = x.Count,
                        }).ToList();
                        break;
                    }

                case TimeGroupBy.Year:
                    {
                        var raw = await baseQ
                            .GroupBy(c => c.CreatedAt.Year)
                            .Select(g => new { Year = g.Key, Count = g.Count() })
                            .OrderBy(x => x.Year)
                            .ToListAsync();
                        items = raw.Select(x => new ContentGrowthPointDto
                        {
                            Period = x.Year.ToString("D4", CultureInfo.InvariantCulture),
                            Contents = x.Count,
                        }).ToList();
                        break;
                    }

                case TimeGroupBy.Week:
                case TimeGroupBy.Quarter:
                default:
                    {
                        var raw = await baseQ
                            .Select(c => new { c.CreatedAt })
                            .ToListAsync();
                        var aggregated = raw
                            .GroupBy(x => query.GroupBy == TimeGroupBy.Week
                                ? GetIsoWeekStartUtc(x.CreatedAt)
                                : GetQuarterStartUtc(x.CreatedAt))
                            .Select(g => new { PeriodStart = g.Key, Count = g.Count() })
                            .OrderBy(x => x.PeriodStart)
                            .ToList();
                        items = aggregated.Select(x => new ContentGrowthPointDto
                        {
                            Period = FormatPeriodLabel(x.PeriodStart, query.GroupBy),
                            Contents = x.Count,
                        }).ToList();
                        break;
                    }
            }

            return Ok(items);
        }

        // CMS: top authors by created content count
        [HttpGet("cms/top-authors")]
        public async Task<ActionResult<List<TopAuthorDto>>> GetTopAuthors([FromQuery] PeriodQuery query, [FromQuery] int limit = 5)
        {
            var (start, end, prev) = ResolveRange(query);

            var q = db.Content!.AsNoTracking()
                .Where(c => c.CreatedAt >= start && c.CreatedAt <= end)
                .GroupBy(c => c.Author)
                .Select(g => new TopAuthorDto { Author = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(limit);

            var list = await q.ToListAsync();

            if (prev.HasValue && list.Count > 0)
            {
                var p = prev.Value;
                var prevDict = await db.Content!.AsNoTracking()
                    .Where(c => c.CreatedAt >= p.from && c.CreatedAt <= p.to)
                    .GroupBy(c => c.Author)
                    .Select(g => new { Author = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Author, x => x.Count);

                foreach (var item in list)
                {
                    if (prevDict.TryGetValue(item.Author, out var pCount) && pCount != 0)
                    {
                        item.ChangePct = (item.Count - pCount) * 100.0 / pCount;
                    }
                }
            }

            return Ok(list);
        }

        // CMS: recent comments
        [HttpGet("cms/recent-comments")]
        public async Task<ActionResult<List<CommentSummaryDto>>> GetRecentComments([FromQuery] int limit = 4)
        {
            var comments = await db.Comments!.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var contentIds = comments.Where(c => c.CommentableType == "Content").Select(c => c.CommentableId).Distinct().ToList();
            var contents = await db.Content!.AsNoTracking().Where(c => contentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Title);

            var result = comments.Select(c => new CommentSummaryDto
            {
                Id = c.Id,
                User = c.AuthorName,
                Comment = c.Body,
                CreatedAt = c.CreatedAt,
                ArticleId = c.CommentableType == "Content" ? c.CommentableId : null,
                Article = c.CommentableType == "Content" && contents.TryGetValue(c.CommentableId, out var t) ? t : null,
            }).ToList();

            return Ok(result);
        }

        // Utility: parse date range from query
        private static (DateTime from, DateTime to, (DateTime from, DateTime to)? prev) ResolveRange(PeriodQuery q)
        {
            DateTime now = DateTime.UtcNow;
            DateTime to = now;
            DateTime from;

            if (q.From.HasValue)
            {
                from = DateTime.SpecifyKind(q.From.Value, DateTimeKind.Utc);
            }
            else
            {
                string p = q.Period?.Trim().ToLowerInvariant() ?? "7d";
                if (p.Length > 1 && p[^1] == 'd' && int.TryParse(p[..^1], out var days))
                {
                    from = to.AddDays(-days);
                }
                else if (p.Length > 1 && p[^1] == 'm' && int.TryParse(p[..^1], out var months))
                {
                    from = to.AddMonths(-months);
                }
                else if (p.EndsWith("yr", StringComparison.Ordinal) && int.TryParse(p[..^2], out var years2))
                {
                    from = to.AddYears(-years2);
                }
                else if (p.Length > 1 && p[^1] == 'y' && int.TryParse(p[..^1], out var years))
                {
                    from = to.AddYears(-years);
                }
                else
                {
                    from = to.AddDays(-7);
                }
            }

            if (q.To.HasValue)
            {
                to = DateTime.SpecifyKind(q.To.Value, DateTimeKind.Utc);
            }

            (DateTime from, DateTime to)? prev = null;
            if (q.Compare)
            {
                var span = to - from;
                var prevTo = from;
                var prevFrom = prevTo - span;
                prev = (prevFrom, prevTo);
            }

            return (from, to, prev);
        }

        private static IQueryable<Contact> ApplyContactFilters(IQueryable<Contact> query, PeriodQuery q)
        {
            if (q.CountryCode.HasValue)
            {
                query = query.Where(c => c.CountryCode == q.CountryCode);
            }

            if (q.AccountId.HasValue)
            {
                query = query.Where(c => c.AccountId == q.AccountId);
            }

            return query;
        }

        // Helpers
        private static string FormatPeriodLabel(DateTime dtUtc, TimeGroupBy g)
        {
            dtUtc = DateTime.SpecifyKind(dtUtc, DateTimeKind.Utc);
            return g switch
            {
                TimeGroupBy.Day => dtUtc.ToString("yyyy-MM-dd"),
                TimeGroupBy.Week => $"{dtUtc:yyyy}-W{ISOWeek.GetWeekOfYear(dtUtc):D2}",
                TimeGroupBy.Month => dtUtc.ToString("yyyy-MM"),
                TimeGroupBy.Quarter => $"{dtUtc:yyyy}-Q{((dtUtc.Month - 1) / 3) + 1}",
                TimeGroupBy.Year => dtUtc.ToString("yyyy"),
                _ => dtUtc.ToString("yyyy-MM"),
            };
        }

        private static DateTime GetIsoWeekStartUtc(DateTime dt)
        {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            // ISO week starts on Monday
            int diff = ((int)dt.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var monday = dt.Date.AddDays(-diff);
            return new DateTime(monday.Year, monday.Month, monday.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        private static DateTime GetQuarterStartUtc(DateTime dt)
        {
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            int quarter = (((dt.Month - 1) / 3) * 3) + 1;
            return new DateTime(dt.Year, quarter, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private static SalesPerformancePointDto MapSalesPoint(string period, double revenue, double refunds, double commissions, int orders)
        {
            return new SalesPerformancePointDto
            {
                Period = period,
                Revenue = (decimal)revenue,
                Refunds = (decimal)refunds,
                Commissions = (decimal)commissions,
                Orders = orders,
            };
        }
    }
}
