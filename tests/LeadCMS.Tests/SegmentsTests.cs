// <copyright file="SegmentsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using LeadCMS.Infrastructure;

namespace LeadCMS.Tests;

public class SegmentsTests : BaseTestAutoLogin
{
    private const string SegmentsUrl = "/api/segments";
    private const string ContactsUrl = "/api/contacts";

    [Fact]
    public async Task CreateStaticSegment_ReturnsContactsAndCount()
    {
        var firstId = await CreateContactAsync("1", "vip1@test.net", "Vip", "One");
        var secondId = await CreateContactAsync("2", "vip2@test.net", "Vip", "Two");
        await CreateContactAsync("3", "other@test.net", "Other", "Contact");

        var segmentDto = new SegmentCreateDto
        {
            Name = "Static segment",
            Type = SegmentType.Static,
            ContactIds = new[] { firstId, secondId },
            Tags = new[] { "vip" },
        };

        var segmentLocation = await PostTest(SegmentsUrl, segmentDto);
        var segment = await GetTest<SegmentDetailsDto>(segmentLocation);

        segment.Should().NotBeNull();
        var segmentValue = segment ?? throw new InvalidOperationException("Expected segment details.");
        segmentValue.Type.Should().Be(SegmentType.Static);
        segmentValue.ContactCount.Should().Be(2);
        segmentValue.ContactIds.Should().BeEquivalentTo(new[] { firstId, secondId });

        var response = await GetTest($"{SegmentsUrl}/{segmentValue.Id}/contacts?query=vip1");
        var totalCountHeader = response.Headers.GetValues(ResponseHeaderNames.TotalCount).FirstOrDefault();
        totalCountHeader.Should().Be("1");

        var content = await response.Content.ReadAsStringAsync();
        var contacts = JsonHelper.Deserialize<List<ContactDetailsDto>>(content);
        contacts.Should().NotBeNull();
        var contactsList = contacts ?? throw new InvalidOperationException("Expected contacts payload.");
        contactsList.Should().ContainSingle();
        contactsList[0].Id.Should().Be(firstId);
    }

    [Fact]
    public async Task CreateDynamicSegment_ComputesContactCountAndFiltersContacts()
    {
        await CreateContactAsync("1", "vip1@test.net", "Ann", "Allowed");
        var targetId = await CreateContactAsync("2", "vip2@test.net", "Ann", "Allowed");
        await CreateContactAsync("3", "regular@test.net", "Ann", "Allowed");

        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "vip" },
                },
            },
        };

        var segmentDto = new SegmentCreateDto
        {
            Name = "Dynamic segment",
            Type = SegmentType.Dynamic,
            Definition = definition,
        };

        var segmentLocation = await PostTest(SegmentsUrl, segmentDto);
        var segment = await GetTest<SegmentDetailsDto>(segmentLocation);

        segment.Should().NotBeNull();
        var segmentValue = segment ?? throw new InvalidOperationException("Expected segment details.");
        segmentValue.ContactCount.Should().Be(2);

        var response = await GetTest($"{SegmentsUrl}/{segmentValue.Id}/contacts?query=vip2");
        var content = await response.Content.ReadAsStringAsync();
        var contacts = JsonHelper.Deserialize<List<ContactDetailsDto>>(content);

        contacts.Should().NotBeNull();
        var contactsList = contacts ?? throw new InvalidOperationException("Expected contacts payload.");
        contactsList.Should().ContainSingle();
        contactsList[0].Id.Should().Be(targetId);
    }

    [Fact]
    public async Task PreviewSegment_RespectsIncludeAndExcludeRules()
    {
        var includedId = await CreateContactAsync("1", "vip1@test.net", "Annabelle", "Allowed");
        await CreateContactAsync("2", "vip2@test.net", "Ann", "Blocked");
        await CreateContactAsync("3", "regular@test.net", "Ann", "Allowed");

        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "vip" },
                    new SegmentRule { FieldId = "firstName", Operator = FieldOperator.StartsWith, Value = "Ann" },
                },
            },
            ExcludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "lastName", Operator = FieldOperator.Equals, Value = "Blocked" },
                },
            },
        };

        var preview = await PostTest<SegmentPreviewResultDto>($"{SegmentsUrl}/preview", definition, HttpStatusCode.OK);

        preview.Should().NotBeNull();
        var previewResult = preview ?? throw new InvalidOperationException("Expected preview result.");
        previewResult.ContactCount.Should().Be(1);
        previewResult.Contacts.Should().ContainSingle();
        previewResult.Contacts[0].Id.Should().Be(includedId);
        previewResult.Contacts[0].AvatarUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PreviewSegment_IsCaseInsensitiveForContainsAndEquals()
    {
        await CreateContactAsync("1", "Case@Test.COM", "Alice", "Alpha");
        await CreateContactAsync("2", "user@test.com", "ALICE", "Beta");
        await CreateContactAsync("3", "nontest@other.com", "Alice", "Gamma");

        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "TEST.COM" },
                    new SegmentRule { FieldId = "firstName", Operator = FieldOperator.Equals, Value = "alice" },
                },
            },
        };

        var preview = await PostTest<SegmentPreviewResultDto>($"{SegmentsUrl}/preview", definition, HttpStatusCode.OK);

        preview.Should().NotBeNull();
        var previewResult = preview ?? throw new InvalidOperationException("Expected preview result.");
        previewResult.ContactCount.Should().Be(2);
        previewResult.Contacts.Should().HaveCount(2);
    }

    [Fact]
    public async Task PreviewSegment_IsCaseInsensitiveForNotContainsAndNotEquals()
    {
        await CreateContactAsync("1", "case1@test.net", "Bob", "Block");
        var allowedId = await CreateContactAsync("2", "case2@test.net", "Alice", "Allow");
        await CreateContactAsync("3", "case3@test.net", "bOb", "Allow");

        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "lastName", Operator = FieldOperator.NotContains, Value = "BLOCK" },
                    new SegmentRule { FieldId = "firstName", Operator = FieldOperator.NotEquals, Value = "BOB" },
                },
            },
        };

        var preview = await PostTest<SegmentPreviewResultDto>($"{SegmentsUrl}/preview", definition, HttpStatusCode.OK);

        preview.Should().NotBeNull();
        var previewResult = preview ?? throw new InvalidOperationException("Expected preview result.");
        previewResult.ContactCount.Should().Be(1);
        previewResult.Contacts.Should().ContainSingle();
        previewResult.Contacts[0].Id.Should().Be(allowedId);
    }

    [Fact]
    public async Task PreviewSegment_ReturnsTotalCountBeyondPageSize()
    {
        const int totalContacts = 120;
        var contacts = new List<Contact>();

        for (var i = 0; i < totalContacts; i++)
        {
            contacts.Add(new Contact
            {
                Email = $"previewvip{i}@test.net",
                FirstName = "Preview",
                LastName = "Vip",
            });
        }

        App.PopulateBulkData<Contact, IContactService>(contacts);

        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "previewvip" },
                },
            },
        };

        var preview = await PostTest<SegmentPreviewResultDto>($"{SegmentsUrl}/preview", definition, HttpStatusCode.OK);

        preview.Should().NotBeNull();
        var previewResult = preview ?? throw new InvalidOperationException("Expected preview result.");
        previewResult.ContactCount.Should().Be(totalContacts);
        previewResult.Contacts.Should().HaveCount(100);
    }

    [Fact]
    public async Task PreviewSegment_SupportsNestedAccountAttributes()
    {
        // Create an account with a specific total revenue
        var account = new Account
        {
            Name = "High Value Account" + Guid.NewGuid().ToString()[..8],
            TotalRevenue = 15000.00m,
        };
        var dbContext = App.GetDbContext();
        dbContext!.Accounts!.Add(account);
        await dbContext.SaveChangesAsync();

        // Create domains
        var domain1 = new Domain { Name = $"highvalue-{Guid.NewGuid().ToString()[..8]}.com" };
        var domain2 = new Domain { Name = $"lowvalue-{Guid.NewGuid().ToString()[..8]}.com" };
        dbContext.Domains!.AddRange(domain1, domain2);
        await dbContext.SaveChangesAsync();

        // Create contacts linked to this account
        var contact1 = new Contact
        {
            Email = $"contact1-{Guid.NewGuid().ToString()[..8]}@{domain1.Name}",
            FirstName = "John",
            LastName = "Doe",
            AccountId = account.Id,
            DomainId = domain1.Id,
        };
        var contact2 = new Contact
        {
            Email = $"contact2-{Guid.NewGuid().ToString()[..8]}@{domain2.Name}",
            FirstName = "Jane",
            LastName = "Smith",
            DomainId = domain2.Id,
            // No AccountId - should be filtered out
        };

        dbContext.Contacts!.AddRange(contact1, contact2);
        await dbContext.SaveChangesAsync();

        // Test filtering by account.totalRevenue > 10
        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "account.totalRevenue", Operator = FieldOperator.GreaterThan, Value = "10" },
                },
            },
        };

        var preview = await PostTest<SegmentPreviewResultDto>($"{SegmentsUrl}/preview", definition, HttpStatusCode.OK);

        preview.Should().NotBeNull();
        var previewResult = preview ?? throw new InvalidOperationException("Expected preview result.");
        previewResult.ContactCount.Should().Be(1);
        previewResult.Contacts.Should().ContainSingle();
        previewResult.Contacts[0].FirstName.Should().Be("John");
    }

    [Fact]
    public async Task GetContacts_ForMissingSegment_ReturnsNotFound()
    {
        await GetTest($"{SegmentsUrl}/999/contacts", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecalculateSegment_DynamicSegment_ShouldUpdateContactCount()
    {
        // Arrange: Create initial contacts
        await CreateContactAsync("recalc1", "recalc1@test.net", "Test", "User");
        await CreateContactAsync("recalc2", "recalc2@test.net", "Test", "User");

        // Create a dynamic segment matching emails containing "recalc"
        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "recalc" },
                },
            },
        };

        var segmentDto = new SegmentCreateDto
        {
            Name = "Recalc Test Segment",
            Type = SegmentType.Dynamic,
            Definition = definition,
        };

        var segmentLocation = await PostTest(SegmentsUrl, segmentDto);
        var createdSegment = await GetTest<SegmentDetailsDto>(segmentLocation);

        createdSegment.Should().NotBeNull();
        createdSegment!.ContactCount.Should().Be(2);

        // Add more contacts that match the segment
        await CreateContactAsync("recalc3", "recalc3@test.net", "Test", "User");
        await CreateContactAsync("recalc4", "recalc4@test.net", "Test", "User");

        // Act: Recalculate segment
        var recalculateUrl = $"{segmentLocation}/recalculate";
        var recalculatedSegment = await PostTest<SegmentDetailsDto>(recalculateUrl, new { }, HttpStatusCode.OK);

        // Assert: Contact count should be updated
        recalculatedSegment.Should().NotBeNull();
        recalculatedSegment!.ContactCount.Should().Be(4);
        recalculatedSegment.Name.Should().Be(createdSegment.Name);
        recalculatedSegment.Type.Should().Be(SegmentType.Dynamic);
    }

    [Fact]
    public async Task RecalculateSegment_StaticSegment_ShouldKeepContactCount()
    {
        // Arrange: Create test contacts
        var firstId = await CreateContactAsync("static-recalc1", "static1@test.net", "Test", "User");
        var secondId = await CreateContactAsync("static-recalc2", "static2@test.net", "Test", "User");

        // Create a static segment
        var segmentDto = new SegmentCreateDto
        {
            Name = "Static Recalc Test",
            Type = SegmentType.Static,
            ContactIds = new[] { firstId, secondId },
        };

        var segmentLocation = await PostTest(SegmentsUrl, segmentDto);
        var createdSegment = await GetTest<SegmentDetailsDto>(segmentLocation);

        createdSegment.Should().NotBeNull();
        createdSegment!.ContactCount.Should().Be(2);

        // Add more contacts (but they shouldn't affect static segment)
        await CreateContactAsync("static-recalc3", "static3@test.net", "Test", "User");

        // Act: Recalculate segment
        var recalculateUrl = $"{segmentLocation}/recalculate";
        var recalculatedSegment = await PostTest<SegmentDetailsDto>(recalculateUrl, new { }, HttpStatusCode.OK);

        // Assert: Contact count should remain the same
        recalculatedSegment.Should().NotBeNull();
        recalculatedSegment!.ContactCount.Should().Be(2);
        recalculatedSegment.ContactIds.Should().BeEquivalentTo(new[] { firstId, secondId });
    }

    [Fact]
    public async Task RecalculateSegment_NonExistentSegment_ShouldReturn404()
    {
        // Arrange
        var nonExistentId = 999999;
        var recalculateUrl = $"{SegmentsUrl}/{nonExistentId}/recalculate";

        // Act & Assert
        await PostTest(recalculateUrl, new { }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecalculateSegment_AfterContactsDeleted_ShouldUpdateCount()
    {
        // Arrange: Create contacts
        await CreateContactAsync("delete1", "delete1@test.net", "Test", "User");
        var deleteId = await CreateContactAsync("delete2", "delete2@test.net", "Test", "User");
        await CreateContactAsync("delete3", "delete3@test.net", "Test", "User");

        // Create segment
        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "delete" },
                },
            },
        };

        var segmentDto = new SegmentCreateDto
        {
            Name = "Delete Test Segment",
            Type = SegmentType.Dynamic,
            Definition = definition,
        };

        var segmentLocation = await PostTest(SegmentsUrl, segmentDto);
        var createdSegment = await GetTest<SegmentDetailsDto>(segmentLocation);

        createdSegment.Should().NotBeNull();
        createdSegment!.ContactCount.Should().Be(3);

        // Delete one contact
        await DeleteTest($"{ContactsUrl}/{deleteId}");

        // Act: Recalculate segment
        var recalculateUrl = $"{segmentLocation}/recalculate";
        var recalculatedSegment = await PostTest<SegmentDetailsDto>(recalculateUrl, new { }, HttpStatusCode.OK);

        // Assert: Contact count should be updated
        recalculatedSegment.Should().NotBeNull();
        recalculatedSegment!.ContactCount.Should().Be(2);
    }

    [Fact]
    public async Task UpdateDynamicSegment_ShouldRecalculateContactCount()
    {
        // Arrange: Create test contacts
        await CreateContactAsync("update1", "update1@test.net", "Test", "User");
        await CreateContactAsync("update2", "update2@test.net", "Test", "User");
        await CreateContactAsync("other1", "other1@test.net", "Test", "User");

        // Create segment matching "update1"
        var definition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "update1" },
                },
            },
        };

        var segmentDto = new SegmentCreateDto
        {
            Name = "Update Test Segment",
            Type = SegmentType.Dynamic,
            Definition = definition,
        };

        var segmentLocation = await PostTest(SegmentsUrl, segmentDto);
        var createdSegment = await GetTest<SegmentDetailsDto>(segmentLocation);

        createdSegment.Should().NotBeNull();
        createdSegment!.ContactCount.Should().Be(1);

        // Act: Update segment to match more contacts
        var newDefinition = new SegmentDefinition
        {
            IncludeRules = new RuleGroup
            {
                Connector = RuleConnector.And,
                Rules = new List<SegmentRule>
                {
                    new SegmentRule { FieldId = "email", Operator = FieldOperator.Contains, Value = "update" },
                },
            },
        };

        var updateDto = new SegmentUpdateDto
        {
            Definition = newDefinition,
        };

        var response = await PatchTest(segmentLocation, updateDto);
        var content = await response.Content.ReadAsStringAsync();
        var updatedSegment = JsonHelper.Deserialize<SegmentDetailsDto>(content);

        // Assert: Contact count should be recalculated
        updatedSegment.Should().NotBeNull();
        updatedSegment!.ContactCount.Should().Be(2);
    }

    private static int ExtractId(string location)
    {
        return int.Parse(location.Split("/").Last());
    }

    private async Task<int> CreateContactAsync(string uid, string email, string firstName, string lastName)
    {
        var contact = TestData.Generate<TestContact>(uid);
        contact.Email = email;
        contact.FirstName = firstName;
        contact.LastName = lastName;

        var location = await PostTest(ContactsUrl, contact);
        return ExtractId(location);
    }
}
