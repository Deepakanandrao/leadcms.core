// <copyright file="ExportNestedPropertiesTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Infrastructure;

namespace LeadCMS.Tests;

/// <summary>
/// Tests for export functionality with nested property field selection.
/// Tests cover CSV exports with flattened nested property names (e.g., contact.fullName → ContactFullName).
/// </summary>
public class ExportNestedPropertiesTests : TableWithFKTests<Order, TestOrder, OrderUpdateDto, IEntityService<Order>>
{
    public ExportNestedPropertiesTests()
        : base("/api/orders")
    {
    }

    /// <summary>
    /// Tests that export with nested property selection creates flattened columns.
    /// Verifies that contact.fullName becomes ContactFullName in CSV output.
    /// </summary>
    [Fact]
    public async Task Export_WithNestedProperty_CreatesFieldInExportedCSV()
    {
        // Arrange - create a contact and order
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var orderDto = TestData.Generate<TestOrder>("test1", fkId);
        orderDto.OrderNumber = "ORD-001";
        orderDto.RefNo = "REF-001";
        await PostTest(itemsUrl, orderDto);

        var dbContext = App.GetDbContext();
        var order = dbContext!.Orders!.First(o => o.RefNo == orderDto.RefNo);

        await SyncElasticSearch();

        // Act - export with nested property field selection
        var exportUrl = $"{itemsUrl}/export?filter[field][orderNumber]=true&filter[field][refNo]=true&filter[field][contact.fullName]=true&filter[ids]={order.Id}";
        var response = await GetTestCSV<OrderExportWithContactFullName>(exportUrl);

        // Assert
        response.Should().NotBeNull();
        response!.Count.Should().Be(1);
        response[0].OrderNumber.Should().Be("ORD-001");
        response[0].RefNo.Should().Be("REF-001");
        response[0].ContactFullName.Should().NotBeNullOrEmpty(); // Contact has full name from test data
    }

    /// <summary>
    /// Tests that export with only nested properties (no main entity properties) works correctly.
    /// Note: This tests an edge case - in practice, exports typically include at least one main property.
    /// </summary>
    [Fact]
    public async Task Export_WithOnlyNestedProperties_ReturnsOnlyNestedFields()
    {
        // Arrange
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var orderDto = TestData.Generate<TestOrder>("test2", fkId);
        await PostTest(itemsUrl, orderDto);

        var dbContext = App.GetDbContext();
        var order = dbContext!.Orders!.First(o => o.RefNo == orderDto.RefNo);

        // Reload to ensure we have the latest data with computed columns
        dbContext.Entry(order).Reload();

        // Also load the contact with includes to verify it has data
        var contactLoaded = dbContext!.Contacts!.First(c => c.Id == fkId);

        // Verify the contact has a FullName
        contactLoaded.FullName.Should().NotBeNullOrEmpty("because the contact was created with FirstName and LastName");

        await SyncElasticSearch();

        // Act - export with only nested property
        var exportUrl = $"{itemsUrl}/export?filter[field][contact.fullName]=true&filter[ids]={order.Id}";

        var request = new HttpRequestMessage(HttpMethod.Get, exportUrl);
        request.Headers.Authorization = GetAuthenticationHeaderValue();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/csv"));
        var httpResponse = await client.SendAsync(request);

        httpResponse.StatusCode.Should().Be(HttpStatusCode.OK, "export should succeed");

        var csvContent = await httpResponse.Content.ReadAsStringAsync();
        csvContent.Should().Contain("ContactFullName", "CSV should have ContactFullName column header");

        var response = await GetTestCSV<OrderExportOnlyContactFullName>(exportUrl);

        // Assert
        response.Should().NotBeNull();
        response!.Count.Should().Be(1);

        // The export completed successfully and returned the ContactFullName column
        // This validates that nested property exports work
    }

    /// <summary>
    /// Tests that export with mixed main and nested properties works correctly.
    /// </summary>
    [Fact]
    public async Task Export_WithMixedMainAndNestedProperties_PreservesData()
    {
        // Arrange
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var orderDto = TestData.Generate<TestOrder>("test3", fkId);
        orderDto.OrderNumber = "ORD-003";
        orderDto.RefNo = "REF-003";
        await PostTest(itemsUrl, orderDto);

        var dbContext = App.GetDbContext();
        var order = dbContext!.Orders!.First(o => o.RefNo == orderDto.RefNo);

        await SyncElasticSearch();

        // Act - export with mixed properties
        var exportUrl = $"{itemsUrl}/export?filter[field][orderNumber]=true&filter[field][contact.fullName]=true&filter[field][refNo]=true&filter[ids]={order.Id}";
        var response = await GetTestCSV<OrderExportMixed>(exportUrl);

        // Assert
        response.Should().NotBeNull();
        response!.Count.Should().Be(1);
        response[0].OrderNumber.Should().Be("ORD-003");
        response[0].ContactFullName.Should().NotBeNullOrEmpty();
        response[0].RefNo.Should().Be("REF-003");
    }

    /// <summary>
    /// Tests that normal queries (without field selection) still work correctly and aren't affected by nested property changes.
    /// </summary>
    [Fact]
    public async Task NormalQuery_WithoutFieldSelection_StillWorks()
    {
        // Arrange
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var orderDto = TestData.Generate<TestOrder>("test4", fkId);
        orderDto.OrderNumber = "ORD-004";
        orderDto.AffiliateName = "NormalQueryTest";
        await PostTest(itemsUrl, orderDto);

        await SyncElasticSearch();

        // Act - normal query without field selection
        var result = await GetTest<List<Order>>($"{itemsUrl}?filter[where][orderNumber][eq]=ORD-004");

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
        result[0].OrderNumber.Should().Be("ORD-004");
        result[0].AffiliateName.Should().Be("NormalQueryTest");
    }

    /// <summary>
    /// Tests that queries with nested property in WHERE clause still work correctly.
    /// </summary>
    [Fact]
    public async Task Query_WithNestedPropertyInWhere_StillWorks()
    {
        // Arrange - create two contacts with different names
        var uniqueId1 = Guid.NewGuid().ToString()[..8];
        var contact1Dto = new TestContact(uniqueId1);
        contact1Dto.FirstName = "Alice";
        contact1Dto.LastName = "Anderson";
        var contact1Url = await PostTest("/api/contacts", contact1Dto, HttpStatusCode.Created);
        var contact1 = await GetTest<Contact>(contact1Url, HttpStatusCode.OK);

        var uniqueId2 = Guid.NewGuid().ToString()[..8];
        var contact2Dto = new TestContact(uniqueId2);
        contact2Dto.FirstName = "Bob";
        contact2Dto.LastName = "Brown";
        var contact2Url = await PostTest("/api/contacts", contact2Dto, HttpStatusCode.Created);
        var contact2 = await GetTest<Contact>(contact2Url, HttpStatusCode.OK);

        var order1Dto = TestData.Generate<TestOrder>("test5a", contact1!.Id);
        order1Dto.OrderNumber = "ORD-005A";
        await PostTest(itemsUrl, order1Dto);

        var order2Dto = TestData.Generate<TestOrder>("test5b", contact2!.Id);
        order2Dto.OrderNumber = "ORD-005B";
        await PostTest(itemsUrl, order2Dto);

        var dbContext = App.GetDbContext();
        var order1 = dbContext!.Orders!.First(o => o.RefNo == order1Dto.RefNo);

        await SyncElasticSearch();

        // Act - query with nested property in WHERE clause (search for Alice)
        var result = await GetTest<List<Order>>($"{itemsUrl}?filter[where][contact.fullName][like]=Alice");

        // Assert
        result.Should().NotBeNull();
        result!.Should().Contain(o => o.Id == order1.Id);
    }

    /// <summary>
    /// Tests that queries with nested property in ORDER BY clause still work correctly.
    /// </summary>
    [Fact]
    public async Task Query_WithNestedPropertyInOrderBy_StillWorks()
    {
        // Arrange - create two contacts with names that will sort differently
        var uniqueId1 = Guid.NewGuid().ToString()[..8];
        var contactZ = new TestContact(uniqueId1);
        contactZ.FirstName = "Zack";
        contactZ.LastName = "Zebra";
        var contactZUrl = await PostTest("/api/contacts", contactZ, HttpStatusCode.Created);
        var contactZEntity = await GetTest<Contact>(contactZUrl, HttpStatusCode.OK);

        var uniqueId2 = Guid.NewGuid().ToString()[..8];
        var contactA = new TestContact(uniqueId2);
        contactA.FirstName = "Amy";
        contactA.LastName = "Apple";
        var contactAUrl = await PostTest("/api/contacts", contactA, HttpStatusCode.Created);
        var contactAEntity = await GetTest<Contact>(contactAUrl, HttpStatusCode.OK);

        var order1Dto = TestData.Generate<TestOrder>("test6a", contactZEntity!.Id);
        order1Dto.OrderNumber = "ORD-006A";
        await PostTest(itemsUrl, order1Dto);

        var order2Dto = TestData.Generate<TestOrder>("test6b", contactAEntity!.Id);
        order2Dto.OrderNumber = "ORD-006B";
        await PostTest(itemsUrl, order2Dto);

        var dbContext = App.GetDbContext();
        var order1 = dbContext!.Orders!.First(o => o.RefNo == order1Dto.RefNo);
        var order2 = dbContext!.Orders!.First(o => o.RefNo == order2Dto.RefNo);

        await SyncElasticSearch();

        // Act - query with nested property in ORDER BY
        var result = await GetTest<List<Order>>($"{itemsUrl}?filter[where][orderNumber][like]=ORD-006&filter[order]=contact.fullName asc");

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result[0].Id.Should().Be(order2.Id); // Amy should be first
        result[1].Id.Should().Be(order1.Id); // Zack should be second
    }

    protected override async Task<(TestOrder, string)> CreateItem(string uid, int fkId)
    {
        var testOrder = new TestOrder(uid, fkId);
        var newUrl = await PostTest(itemsUrl, testOrder);
        return (testOrder, newUrl);
    }

    protected override OrderUpdateDto UpdateItem(TestOrder to)
    {
        var from = new OrderUpdateDto();
        to.RefNo = from.RefNo = to.RefNo + "1";
        return from;
    }

    protected override async Task<(int, string)> CreateFKItem()
    {
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var fkItemCreate = new TestContact(uniqueId);
        var fkUrl = await PostTest("/api/contacts", fkItemCreate, HttpStatusCode.Created);
        var fkItem = await GetTest<Contact>(fkUrl, HttpStatusCode.OK);
        fkItem.Should().NotBeNull();
        return (fkItem!.Id, fkUrl);
    }

    // DTOs for CSV parsing
    public class OrderExportWithContactFullName
    {
        public string? OrderNumber { get; set; }

        public string? RefNo { get; set; }

        public string? ContactFullName { get; set; }
    }

    public class OrderExportOnlyContactFullName
    {
        public string? ContactFullName { get; set; }
    }

    public class OrderExportMixed
    {
        public string? OrderNumber { get; set; }

        public string? ContactFullName { get; set; }

        public string? RefNo { get; set; }
    }
}
