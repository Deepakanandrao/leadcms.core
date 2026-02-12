// <copyright file="OrderItemService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;

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
                await pgDbContext.OrderItems!.AddAsync(orderItem);
            }

            orderService.RecalculateOrder(orderItem.Order!);
        }

        public Task SaveRangeAsync(List<OrderItem> items)
        {
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
                return pgDbContext.OrderItems!.AddRangeAsync(@new).ContinueWith(_ =>
                {
                    // Recalculate parent orders after batch insert
                    foreach (var order in items.Select(i => i.Order!).Where(o => o != null).Distinct())
                    {
                        orderService.RecalculateOrder(order);
                    }
                });
            }

            // Recalculate parent orders after updates
            foreach (var order in items.Select(i => i.Order!).Where(o => o != null).Distinct())
            {
                orderService.RecalculateOrder(order);
            }

            return Task.CompletedTask;
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
    }
}