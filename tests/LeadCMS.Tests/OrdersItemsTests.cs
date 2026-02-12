// <copyright file="OrdersItemsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;

namespace LeadCMS.Tests;

public class OrdersItemsTests : TableWithFKTests<OrderItem, TestOrderItem, OrderItemUpdateDto, IEntityService<OrderItem>>
{
    public OrdersItemsTests()
        : base("/api/order-items")
    {
    }

    [Fact]
    public async Task OrderQuantityTest()
    {
        var orderDetails = await CreateFKItem();

        var numberOfOrderItems = 10;

        var sumQuantity = 0;
        var orderItemsUrls = new string[numberOfOrderItems];

        for (var i = 0; i < numberOfOrderItems; i++)
        {
            var quantity = i + 1;
            sumQuantity += quantity;

            var testOrderItem = new TestOrderItem(string.Empty, orderDetails.Item1)
            {
                Quantity = quantity,
            };

            var newUrl = await PostTest(itemsUrl, testOrderItem);

            orderItemsUrls[i] = newUrl;
        }

        var updatedOrder = await GetTest<OrderDetailsDto>(orderDetails.Item2);

        updatedOrder.Should().NotBeNull();

        if (updatedOrder != null)
        {
            (updatedOrder.Quantity == sumQuantity).Should().BeTrue();
        }

        var addedOrderItem = await GetTest<OrderItem>(orderItemsUrls[0]);
        addedOrderItem.Should().NotBeNull();

        if (addedOrderItem != null)
        {
            var orderItem = new OrderItemUpdateDto
            {
                Quantity = addedOrderItem.Quantity + 999,
            };

            await PatchTest(orderItemsUrls[0], orderItem);

            updatedOrder = await GetTest<OrderDetailsDto>(orderDetails.Item2);
            updatedOrder.Should().NotBeNull();

            if (updatedOrder != null)
            {
                (updatedOrder.Quantity == sumQuantity + 999).Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task OrderTotalTest()
    {
        var orderDetails = await CreateFKItem();

        var numberOfOrderItems = 10;

        var orderItemsUrls = new string[numberOfOrderItems];

        for (var i = 0; i < numberOfOrderItems; ++i)
        {
            var orderItem = new TestOrderItem(string.Empty, orderDetails.Item1)
            {
                Quantity = i + 1,
            };

            var orderItemUrl = await PostTest(itemsUrl, orderItem);
            orderItemsUrls[i] = orderItemUrl;
        }

        async Task CompareTotals()
        {
            var total = 0m;
            foreach (var url in orderItemsUrls)
            {
                var orderItem = await GetTest<OrderItemDetailsDto>(url);
                orderItem.Should().NotBeNull();

                if (orderItem != null)
                {
                    total += orderItem.Total;
                }
            }

            var updatedOrder = await GetTest<OrderDetailsDto>(orderDetails.Item2);
            updatedOrder.Should().NotBeNull();

            if (updatedOrder != null)
            {
                updatedOrder.Total.Should().Be(total);
            }
        }

        await CompareTotals();

        var addedOrderItem = await GetTest<OrderItemDetailsDto>(orderItemsUrls[0]);
        addedOrderItem.Should().NotBeNull();

        var updatedOrderItem = new OrderItemUpdateDto();
        if (addedOrderItem != null)
        {
            updatedOrderItem.UnitPrice = addedOrderItem.UnitPrice;
            updatedOrderItem.Quantity = addedOrderItem.Quantity + 999;
        }

        await PatchTest(orderItemsUrls[0], updatedOrderItem);
        await CompareTotals();

        addedOrderItem = await GetTest<OrderItemDetailsDto>(orderItemsUrls[0]);
        addedOrderItem.Should().NotBeNull();
        updatedOrderItem = new OrderItemUpdateDto();
        if (addedOrderItem != null)
        {
            updatedOrderItem.UnitPrice = addedOrderItem.UnitPrice + new decimal(1.0);
            updatedOrderItem.Quantity = addedOrderItem.Quantity;
        }

        await PatchTest(orderItemsUrls[0], updatedOrderItem);
        await CompareTotals();
    }

    [Fact]
    public async Task OrderItemTotalTest()
    {
        var orderDetails = await CreateFKItem();

        var orderItem = new TestOrderItem(string.Empty, orderDetails.Item1);

        var orderItemUrl = await PostTest(itemsUrl, orderItem);

        var order = await GetTest<Order>(orderDetails.Item2);

        order.Should().NotBeNull();

        async Task CompareItems()
        {
            var addedOrderItem = await GetTest<OrderItemDetailsDto>(orderItemUrl);
            addedOrderItem.Should().NotBeNull();

            if (addedOrderItem != null)
            {
                addedOrderItem.CurrencyTotal.Should().Be(orderItem.Quantity * orderItem.UnitPrice);
                addedOrderItem.Total.Should().Be(addedOrderItem.CurrencyTotal * order!.ExchangeRate);
            }
        }

        await CompareItems();

        var addedOrderItem = await GetTest<OrderItemDetailsDto>(orderItemUrl);
        addedOrderItem.Should().NotBeNull();

        var updatedOrderItem = new OrderItemUpdateDto();

        if (addedOrderItem != null)
        {
            updatedOrderItem.Quantity = addedOrderItem.Quantity + 10;
            updatedOrderItem.UnitPrice = addedOrderItem.UnitPrice;
        }

        await PatchTest(orderItemUrl, orderItem);
        await CompareItems();

        addedOrderItem = await GetTest<OrderItemDetailsDto>(orderItemUrl);
        addedOrderItem.Should().NotBeNull();

        updatedOrderItem = new OrderItemUpdateDto();

        if (addedOrderItem != null)
        {
            updatedOrderItem.Quantity = addedOrderItem.Quantity;
            updatedOrderItem.UnitPrice = addedOrderItem.UnitPrice + new decimal(1.0);
        }

        await PatchTest(orderItemUrl, orderItem);
        await CompareItems();
    }

    [Fact]
    public async Task OrderItemTotalWithPrimaryCurrencyTest()
    {
        var orderDetails = await CreateFKItemWithPrimaryCurrency();

        var orderItem = new TestOrderItem(string.Empty, orderDetails.Item1);

        var orderItemUrl = await PostTest(itemsUrl, orderItem);

        var addedOrderItem = await GetTest<OrderItemDetailsDto>(orderItemUrl);
        addedOrderItem.Should().NotBeNull();

        if (addedOrderItem != null)
        {
            addedOrderItem.CurrencyTotal.Should().Be(orderItem.Quantity * orderItem.UnitPrice);

            // When order currency matches the primary currency (USD), exchange rate is treated as 1
            addedOrderItem.Total.Should().Be(addedOrderItem.CurrencyTotal);
        }
    }

    [Fact]
    public async Task ShouldNotUpdateTotalsTest()
    {
        var addedOrderItemDetails = await CreateItem();
        var orderItemUrl = addedOrderItemDetails.Item2;
        var addedOrderItem = await GetTest<OrderItem>(orderItemUrl);
        addedOrderItem.Should().NotBeNull();

        if (addedOrderItem != null)
        {
            var updateOrderItemT = new TestOrderItemUpdateWithTotal();
            updateOrderItemT.Total = addedOrderItem.Total + 1.0m;
            await Patch(orderItemUrl, updateOrderItemT);

            var updatedOrderItem = await GetTest<OrderItem>(orderItemUrl);
            updatedOrderItem.Should().NotBeNull();

            if (updatedOrderItem != null)
            {
                updatedOrderItem.Total.Should().Be(addedOrderItem.Total);
            }

            var updateOrderItemCT = new TestOrderItemUpdateWithCurrencyTotal();
            updateOrderItemCT.CurrencyTotal = addedOrderItem.CurrencyTotal + 1.0m;
            await Patch(orderItemUrl, updateOrderItemCT);

            updatedOrderItem = await GetTest<OrderItem>(orderItemUrl);
            updatedOrderItem.Should().NotBeNull();

            if (updatedOrderItem != null)
            {
                updatedOrderItem.CurrencyTotal.Should().Be(addedOrderItem.CurrencyTotal);
            }
        }
    }

    [Theory]
    [InlineData("orderItems.csv", 3)]
    [InlineData("orderItems.json", 3)]
    public async Task ImportFileAddUpdateTest(string fileName, int expectedCount)
    {
        await CreateItem(string.Empty, (await CreateFKItemWithKnownRefNo()).Item1);
        await PostImportTest(itemsUrl, fileName);

        var allOrderItemsResponse = await GetTest(itemsUrl);
        allOrderItemsResponse.Should().NotBeNull();

        var content = await allOrderItemsResponse.Content.ReadAsStringAsync();
        var allOrderItems = JsonSerializer.Deserialize<List<OrderItem>>(content);
        allOrderItems.Should().NotBeNull();
        allOrderItems!.Count.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData("orderItemsNoRef.csv", 1)]
    [InlineData("orderItemsNoRef.json", 1)]
    public async Task ImportFileNoOrderRefNotFoundTest(string fileName, int expectedCount)
    {
        await CreateItem();
        var importResult = await PostImportTest(itemsUrl, fileName, HttpStatusCode.OK);

        importResult.Added.Should().Be(0);
        importResult.Failed.Should().Be(1);

        var allOrderItemsResponse = await GetTest(itemsUrl);
        allOrderItemsResponse.Should().NotBeNull();

        var content = await allOrderItemsResponse.Content.ReadAsStringAsync();
        var allOrderItems = JsonSerializer.Deserialize<List<OrderItem>>(content);
        allOrderItems.Should().NotBeNull();
        allOrderItems!.Count.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData("orderItemsCompositeKey.csv")]
    [InlineData("orderItemsCompositeKey.json")]
    public async Task ImportCompositeKeyCreatesItemsTest(string fileName)
    {
        await CreateFKItemWithKnownRefNo();

        var importResult = await PostImportTest(itemsUrl, fileName);

        importResult.Added.Should().Be(3);
        importResult.Updated.Should().Be(0);
        importResult.Failed.Should().Be(0);

        var allOrderItemsResponse = await GetTest(itemsUrl);
        var content = await allOrderItemsResponse.Content.ReadAsStringAsync();
        var allOrderItems = JsonSerializer.Deserialize<List<OrderItemDetailsDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        allOrderItems.Should().NotBeNull();
        allOrderItems!.Count.Should().Be(3);

        allOrderItems.Should().Contain(x => x.LineNumber == 1 && x.ProductName == "compositeProduct1");
        allOrderItems.Should().Contain(x => x.LineNumber == 2 && x.ProductName == "compositeProduct2");
        allOrderItems.Should().Contain(x => x.LineNumber == 3 && x.ProductName == "compositeProduct3");
    }

    [Theory]
    [InlineData("orderItemsCompositeKey.csv", "orderItemsCompositeKeyUpdate.csv")]
    [InlineData("orderItemsCompositeKey.json", "orderItemsCompositeKeyUpdate.json")]
    public async Task ImportCompositeKeyUpdatesExistingItemsTest(string initialFile, string updateFile)
    {
        await CreateFKItemWithKnownRefNo();

        // First import: creates 3 items with line numbers 1, 2, 3
        var firstResult = await PostImportTest(itemsUrl, initialFile);
        firstResult.Added.Should().Be(3);

        // Second import: lines 1 and 2 should update, line 4 should be new
        var secondResult = await PostImportTest(itemsUrl, updateFile);
        secondResult.Updated.Should().Be(2);
        secondResult.Added.Should().Be(1);
        secondResult.Failed.Should().Be(0);

        var allOrderItemsResponse = await GetTest(itemsUrl);
        var content = await allOrderItemsResponse.Content.ReadAsStringAsync();
        var allOrderItems = JsonSerializer.Deserialize<List<OrderItemDetailsDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        allOrderItems.Should().NotBeNull();

        // Total: 3 original + 1 new = 4 (lines 1, 2 updated in place, line 3 untouched, line 4 new)
        allOrderItems!.Count.Should().Be(4);

        // Verify updated items have new product names
        allOrderItems.Should().Contain(x => x.LineNumber == 1 && x.ProductName == "compositeProduct1Updated");
        allOrderItems.Should().Contain(x => x.LineNumber == 2 && x.ProductName == "compositeProduct2Updated");

        // Verify line 3 still exists from initial import (not touched by update)
        allOrderItems.Should().Contain(x => x.LineNumber == 3 && x.ProductName == "compositeProduct3");

        // Verify new item was added
        allOrderItems.Should().Contain(x => x.LineNumber == 4 && x.ProductName == "compositeProduct4New");
    }

    [Theory]
    [InlineData("orderItemsCompositeKeyDuplicates.csv")]
    [InlineData("orderItemsCompositeKeyDuplicates.json")]
    public async Task ImportCompositeKeyDuplicatesInBatchTest(string fileName)
    {
        await CreateFKItemWithKnownRefNo();

        var importResult = await PostImportTest(itemsUrl, fileName);

        // Row with line 1 appears twice — the second occurrence should be flagged as duplicate
        importResult.Added.Should().Be(2);
        importResult.Failed.Should().Be(1);
        importResult.Errors.Should().NotBeNull();
        importResult.Errors!.Count.Should().Be(1);

        var allOrderItemsResponse = await GetTest(itemsUrl);
        var content = await allOrderItemsResponse.Content.ReadAsStringAsync();
        var allOrderItems = JsonSerializer.Deserialize<List<OrderItemDetailsDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        allOrderItems.Should().NotBeNull();
        allOrderItems!.Count.Should().Be(2);

        // Only the first occurrence of line 1 should be imported
        allOrderItems.Should().Contain(x => x.LineNumber == 1 && x.ProductName == "compositeProduct1");
        allOrderItems.Should().Contain(x => x.LineNumber == 2 && x.ProductName == "compositeProduct2");
    }

    [Theory]
    [InlineData("orderItemsCompositeKey.csv")]
    [InlineData("orderItemsCompositeKey.json")]
    public async Task ImportCompositeKeyReimportSameDataIsIdempotentTest(string fileName)
    {
        await CreateFKItemWithKnownRefNo();

        // First import
        var firstResult = await PostImportTest(itemsUrl, fileName);
        firstResult.Added.Should().Be(3);

        // Re-import same file — all items match by composite key but data is identical,
        // so EF Core detects no actual changes and they are skipped
        var secondResult = await PostImportTest(itemsUrl, fileName);
        secondResult.Added.Should().Be(0);
        secondResult.Failed.Should().Be(0);
        secondResult.Skipped.Should().Be(3);

        var allOrderItemsResponse = await GetTest(itemsUrl);
        var content = await allOrderItemsResponse.Content.ReadAsStringAsync();
        var allOrderItems = JsonSerializer.Deserialize<List<OrderItemDetailsDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        allOrderItems.Should().NotBeNull();
        allOrderItems!.Count.Should().Be(3);
    }

    protected override void MustBeEquivalent(object? expected, object? result)
    {
        // Exclude auto-assigned fields that don't exist on the create DTO
        result.Should().BeEquivalentTo(expected, options => options
            .Excluding(ctx => ctx.Path == "LineNumber"));
    }

    protected override void GenerateBulkRecords(int dataCount, Action<TestOrderItem>? populateAttributes = null)
    {
        var fkItem = CreateFKItem().Result;
        var fkId = fkItem.Item1;

        var bulkList = TestData.GenerateAndPopulateAttributes<TestOrderItem>(dataCount, populateAttributes, fkId);
        var bulkEntitiesList = mapper.Map<List<OrderItem>>(bulkList);

        // Assign sequential line numbers to avoid unique constraint violations
        for (var i = 0; i < bulkEntitiesList.Count; i++)
        {
            bulkEntitiesList[i].LineNumber = i + 1;
        }

        PopulateBulkData<OrderItem, IEntityService<OrderItem>>(bulkEntitiesList);
    }

    protected override async Task<(TestOrderItem, string)> CreateItem(string uid, int fkId)
    {
        var testOrderItem = new TestOrderItem(uid, fkId);

        var newUrl = await PostTest(itemsUrl, testOrderItem);

        return (testOrderItem, newUrl);
    }

    protected override OrderItemUpdateDto UpdateItem(TestOrderItem to)
    {
        var from = new OrderItemUpdateDto();
        to.ProductName = from.ProductName = to.ProductName + "Updated";
        return from;
    }

    protected override async Task<(int, string)> CreateFKItem()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];

        var contactCreate = new TestContact(uid);

        var contactUrl = await PostTest("/api/contacts", contactCreate, HttpStatusCode.Created);

        var contact = await GetTest<Contact>(contactUrl);

        contact.Should().NotBeNull();

        var orderCreate = new TestOrder(uid, contact!.Id);

        var orderUrl = await PostTest("/api/orders", orderCreate, HttpStatusCode.Created);

        var order = await GetTest<Order>(orderUrl);

        order.Should().NotBeNull();

        return (order!.Id, orderUrl);
    }

    private async Task<(int, string)> CreateFKItemWithKnownRefNo()
    {
        var contactCreate = new TestContact();

        var contactUrl = await PostTest("/api/contacts", contactCreate, HttpStatusCode.Created);

        var contact = await GetTest<Contact>(contactUrl);

        contact.Should().NotBeNull();

        var orderCreate = new TestOrder(string.Empty, contact!.Id);

        var orderUrl = await PostTest("/api/orders", orderCreate, HttpStatusCode.Created);

        var order = await GetTest<Order>(orderUrl);

        order.Should().NotBeNull();

        return (order!.Id, orderUrl);
    }

    private async Task<(int, string)> CreateFKItemWithPrimaryCurrency()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];

        var contactCreate = new TestContact(uid);

        var contactUrl = await PostTest("/api/contacts", contactCreate, HttpStatusCode.Created);

        var contact = await GetTest<Contact>(contactUrl);

        contact.Should().NotBeNull();

        var orderCreate = new TestOrder(uid, contact!.Id)
        {
            Currency = "USD",
            ExchangeRate = 1m,
        };

        var orderUrl = await PostTest("/api/orders", orderCreate, HttpStatusCode.Created);

        var order = await GetTest<Order>(orderUrl);

        order.Should().NotBeNull();

        return (order!.Id, orderUrl);
    }
}