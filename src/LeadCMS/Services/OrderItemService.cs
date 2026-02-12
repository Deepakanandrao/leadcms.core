// <copyright file="OrderItemService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services
{
    public class OrderItemService : IOrderItemService
    {
        private readonly IOrderService orderService;
        private readonly IConfiguration configuration;
        private PgDbContext pgDbContext;

        public OrderItemService(PgDbContext pgDbContext, IOrderService orderService, IConfiguration configuration)
        {
            this.pgDbContext = pgDbContext;
            this.orderService = orderService;
            this.configuration = configuration;
        }

        public void Delete(OrderItem orderItem)
        {
            pgDbContext.Remove(orderItem);
            orderService.RecalculateOrder(orderItem.Order!);
        }

        public async Task SaveAsync(OrderItem orderItem)
        {
            orderItem.CurrencyTotal = CalculateOrderItemCurrencyTotal(orderItem);
            orderItem.Total = CalculateOrderItemTotal(orderItem, orderItem.Order!);

            if (orderItem.Id > 0)
            {
                pgDbContext.OrderItems!.Update(orderItem);
            }
            else
            {
                if (orderItem.LineNumber == 0)
                {
                    orderItem.LineNumber = await GetNextLineNumberAsync(orderItem.OrderId);
                }

                await pgDbContext.OrderItems!.AddAsync(orderItem);
            }

            orderService.RecalculateOrder(orderItem.Order!);
        }

        public async Task SaveRangeAsync(List<OrderItem> items)
        {
            // Ensure Order navigation property is loaded for total calculations
            var orderIds = items
                .Where(i => i.Order == null && i.OrderId > 0)
                .Select(i => i.OrderId)
                .Distinct()
                .ToList();

            if (orderIds.Count > 0)
            {
                var orders = await pgDbContext.Orders!
                    .Where(o => orderIds.Contains(o.Id))
                    .ToDictionaryAsync(o => o.Id);

                foreach (var item in items.Where(i => i.Order == null && i.OrderId > 0))
                {
                    if (orders.TryGetValue(item.OrderId, out var order))
                    {
                        item.Order = order;
                    }
                }
            }

            // Calculate totals for each item and persist in batch, mirroring SaveAsync
            foreach (var item in items)
            {
                item.CurrencyTotal = CalculateOrderItemCurrencyTotal(item);
                item.Total = CalculateOrderItemTotal(item, item.Order!);
            }

            var existing = items.Where(i => i.Id > 0).ToList();
            var @new = items.Where(i => i.Id == 0).ToList();

            if (existing.Count > 0)
            {
                pgDbContext.OrderItems!.UpdateRange(existing);
            }

            if (@new.Count > 0)
            {
                // Ensure OrderId is set from navigation property before line number assignment.
                // During import, OrderId may still be 0 if it was resolved via surrogate FK (e.g. OrderRefNo).
                foreach (var item in @new.Where(i => i.OrderId == 0 && i.Order != null))
                {
                    item.OrderId = item!.Order!.Id;
                }

                // Auto-assign LineNumber for new items that don't have one
                await AssignLineNumbersAsync(@new);

                await pgDbContext.OrderItems!.AddRangeAsync(@new);
            }

            // Recalculate parent orders
            foreach (var order in items.Select(i => i.Order!).Where(o => o != null).Distinct())
            {
                orderService.RecalculateOrder(order);
            }
        }

        public void SetDBContext(PgDbContext pgDbContext)
        {
            this.pgDbContext = pgDbContext;
            orderService.SetDBContext(pgDbContext);
        }

        private decimal CalculateOrderItemCurrencyTotal(OrderItem orderItem)
        {
            return orderItem.UnitPrice * orderItem.Quantity;
        }

        private decimal CalculateOrderItemTotal(OrderItem orderItem, Order order)
        {
            var exchangeRate = ResolveExchangeRate(order);
            return orderItem.CurrencyTotal * exchangeRate;
        }

        private decimal ResolveExchangeRate(Order order)
        {
            var primaryCurrency = CurrencyInfoHelper.GetPrimaryCurrencyCode(configuration);
            if (!string.IsNullOrWhiteSpace(order.Currency)
                && string.Equals(order.Currency, primaryCurrency, StringComparison.OrdinalIgnoreCase))
            {
                return 1m;
            }

            return order.ExchangeRate;
        }

        private async Task<int> GetNextLineNumberAsync(int orderId)
        {
            var maxLineNumber = await pgDbContext.OrderItems!
                .Where(oi => oi.OrderId == orderId)
                .MaxAsync(oi => (int?)oi.LineNumber) ?? 0;

            return maxLineNumber + 1;
        }

        private async Task AssignLineNumbersAsync(List<OrderItem> newItems)
        {
            var orderIds = newItems
                .Where(i => i.LineNumber == 0)
                .Select(i => i.OrderId)
                .Distinct()
                .ToList();

            if (orderIds.Count == 0)
            {
                return;
            }

            var maxLineNumbers = await pgDbContext.OrderItems!
                .Where(oi => orderIds.Contains(oi.OrderId))
                .GroupBy(oi => oi.OrderId)
                .Select(g => new { OrderId = g.Key, MaxLineNumber = g.Max(oi => oi.LineNumber) })
                .ToDictionaryAsync(x => x.OrderId, x => x.MaxLineNumber);

            foreach (var item in newItems.Where(i => i.LineNumber == 0))
            {
                maxLineNumbers.TryGetValue(item.OrderId, out var current);
                current++;
                maxLineNumbers[item.OrderId] = current;
                item.LineNumber = current;
            }
        }
    }
}