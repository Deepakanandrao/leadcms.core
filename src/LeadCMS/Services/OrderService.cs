// <copyright file="OrderService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;

namespace LeadCMS.Services
{
    public class OrderService : IOrderService
    {
        private readonly IConfiguration configuration;
        private PgDbContext pgDbContext;

        public OrderService(PgDbContext pgDbContext, IConfiguration configuration)
        {
            this.pgDbContext = pgDbContext;
            this.configuration = configuration;
        }

        public void RecalculateOrder(Order order)
        {
            if (order.OrderItems == null)
            {
                pgDbContext.Entry(order).Collection(o => o.OrderItems!).Load();
            }

            if (order.Discounts == null)
            {
                pgDbContext.Entry(order).Collection(o => o.Discounts!).Load();
            }

            var itemsCurrencyTotalSum = order.OrderItems!.Sum(oi => oi.CurrencyTotal);
            order.CurrencyTotal = itemsCurrencyTotalSum - order.Discounts!.Sum(d => d.Value) - order.Refund;

            var exchangeRate = ResolveExchangeRate(order);
            order.Total = order.CurrencyTotal * exchangeRate;
            order.Quantity = order.OrderItems!.Sum(oi => oi.Quantity);
        }

        public async Task SaveAsync(Order order)
        {
            RecalculateOrder(order);

            if (order.Id > 0)
            {
                pgDbContext.Orders!.Update(order);
            }
            else
            {
                await pgDbContext.Orders!.AddAsync(order);
            }
        }

        public Task SaveRangeAsync(List<Order> items)
        {
            // Recalculate and persist a range of orders with the same rules as SaveAsync
            foreach (var order in items)
            {
                RecalculateOrder(order);
            }

            var existing = items.Where(o => o.Id > 0).ToList();
            var @new = items.Where(o => o.Id == 0).ToList();

            if (existing.Count > 0)
            {
                pgDbContext.Orders!.UpdateRange(existing);
            }

            if (@new.Count > 0)
            {
                return pgDbContext.Orders!.AddRangeAsync(@new);
            }

            return Task.CompletedTask;
        }

        public void SetDBContext(PgDbContext pgDbContext)
        {
            this.pgDbContext = pgDbContext;
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