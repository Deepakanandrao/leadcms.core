// <copyright file="TestOrder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace LeadCMS.Tests.TestEntities;

public class TestOrder : OrderCreateDto
{
    public TestOrder(string uid = "", int contactId = 0)
    {
        RefNo = $"1000{uid}";
        Currency = "EUR";
        ExchangeRate = 1.234M;
        ContactId = contactId;
        Status = OrderStatus.Pending;
    }
}

public class TestOrderWithQuantity : TestOrder
{
    [Required]
    public int Quantity { get; set; } = 10;
}

public class TestOrderWithTotal : TestOrder
{
    [Required]
    public decimal Total { get; set; } = 10;
}

public class TestOrderWithCurrencyTotal : TestOrder
{
    [Required]
    public decimal CurrencyTotal { get; set; } = 10;
}