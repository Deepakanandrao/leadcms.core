// <copyright file="OrderAggregatesTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Reflection;
using LeadCMS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class OrderAggregatesTests : BaseTest
{
    [Fact]
    public void Contact_order_aggregates_are_updated_by_triggers()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var domain = new Domain { Name = "contagg.test" };
        db.Domains!.Add(domain);
        db.SaveChanges();

        var contact = new Contact
        {
            Email = "contact@contagg.test",
            DomainId = domain.Id,
        };

        db.Contacts!.Add(contact);
        db.SaveChanges();

        var firstDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var secondDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        var firstOrder = CreateOrder(contact.Id, "ref-contact-1", 120.50m, firstDate);
        var secondOrder = CreateOrder(contact.Id, "ref-contact-2", 79.50m, secondDate);

        db.Orders!.AddRange(firstOrder, secondOrder);
        db.SaveChanges();

        db.Entry(contact).Reload();

        contact.OrdersCount.Should().Be(2);
        contact.TotalRevenue.Should().Be(200.00m);
        contact.LastOrderDate.Should().Be(secondDate);
    }

    [Fact]
    public void Account_order_aggregates_follow_contact_moves()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var accountA = new Account { Name = "Account A" };
        var accountB = new Account { Name = "Account B" };
        var domain = new Domain { Name = "accountagg.test", Account = accountA };

        db.Accounts!.AddRange(accountA, accountB);
        db.Domains!.Add(domain);
        db.SaveChanges();

        var contact = new Contact
        {
            Email = "contact@accountagg.test",
            DomainId = domain.Id,
            AccountId = accountA.Id,
        };

        db.Contacts!.Add(contact);
        db.SaveChanges();

        var orderDate = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var order = CreateOrder(contact.Id, "ref-account-1", 40m, orderDate);
        db.Orders!.Add(order);
        db.SaveChanges();

        db.Entry(accountA).Reload();
        accountA.OrdersCount.Should().Be(1);
        accountA.TotalRevenue.Should().Be(40m);
        accountA.LastOrderDate.Should().Be(orderDate);

        contact.AccountId = accountB.Id;
        db.SaveChanges();

        db.Entry(accountA).Reload();
        db.Entry(accountB).Reload();

        accountA.OrdersCount.Should().Be(0);
        accountA.TotalRevenue.Should().Be(0m);
        accountA.LastOrderDate.Should().BeNull();

        accountB.OrdersCount.Should().Be(1);
        accountB.TotalRevenue.Should().Be(40m);
        accountB.LastOrderDate.Should().Be(orderDate);
    }

    private static Order CreateOrder(int contactId, string refNo, decimal total, DateTime createdAt)
    {
        var order = new Order
        {
            ContactId = contactId,
            RefNo = refNo,
            Currency = "USD",
            ExchangeRate = 1m,
            CreatedAt = createdAt,
        };

        SetOrderFinancials(order, total, 1);

        return order;
    }

    private static void SetOrderFinancials(Order order, decimal total, int quantity)
    {
        SetProperty(order, nameof(Order.Total), total);
        SetProperty(order, nameof(Order.CurrencyTotal), total);
        SetProperty(order, nameof(Order.Quantity), quantity);
    }

    private static void SetProperty<T>(Order order, string propertyName, T value)
    {
        var property = typeof(Order).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var setter = property?.GetSetMethod(true);
        setter.Should().NotBeNull();
        setter!.Invoke(order, new object[] { value! });
    }
}
