// <copyright file="ContactFullNameTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests;

public class ContactFullNameTests : BaseTestAutoLogin
{
    private const string ContactsUrl = "/api/contacts";

    [Fact]
    public async Task FullName_ComputesWithAllNameParts()
    {
        // Arrange: Create contact with all name parts
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var contactDto = TestData.Generate<TestContact>(uniqueId);
        contactDto.FirstName = "John";
        contactDto.MiddleName = "Robert";
        contactDto.LastName = "Smith";

        // Act
        await PostTest<Contact>(ContactsUrl, contactDto);

        // Get the contact back
        var dbContext = App.GetDbContext();
        var contact = dbContext!.Contacts!.First(c => c.Email == contactDto.Email);

        // Assert
        contact.FullName.Should().Be("John Robert Smith");
    }

    [Fact]
    public async Task FullName_ComputesWithFirstAndLastOnly()
    {
        // Arrange: Create contact without middle name
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var contactDto = TestData.Generate<TestContact>(uniqueId);
        contactDto.FirstName = "Jane";
        contactDto.MiddleName = null;
        contactDto.LastName = "Doe";

        // Act
        await PostTest<Contact>(ContactsUrl, contactDto);

        // Get the contact back
        var dbContext = App.GetDbContext();
        var contact = dbContext!.Contacts!.First(c => c.Email == contactDto.Email);

        // Assert
        contact.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task FullName_ComputesWithFirstNameOnly()
    {
        // Arrange: Create contact with only first name
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var contactDto = TestData.Generate<TestContact>(uniqueId);
        contactDto.FirstName = "Madonna";
        contactDto.MiddleName = null;
        contactDto.LastName = null;

        // Act
        await PostTest<Contact>(ContactsUrl, contactDto);

        // Get the contact back
        var dbContext = App.GetDbContext();
        var contact = dbContext!.Contacts!.First(c => c.Email == contactDto.Email);

        // Assert
        contact.FullName.Should().Be("Madonna");
    }

    [Fact]
    public async Task FullName_UpdatesWhenNameChanges()
    {
        // Arrange: Create contact
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var contactDto = TestData.Generate<TestContact>(uniqueId);
        contactDto.FirstName = "Alice";
        contactDto.LastName = "Brown";
        await PostTest<Contact>(ContactsUrl, contactDto);

        var dbContext = App.GetDbContext();
        var contact = dbContext!.Contacts!.First(c => c.Email == contactDto.Email);
        contact.FullName.Should().Be("Alice Brown");

        // Act: Update the name
        contact.FirstName = "Alicia";
        contact.MiddleName = "Marie";
        await dbContext.SaveChangesAsync();

        // Refresh from database
        dbContext.Entry(contact).Reload();

        // Assert: FullName should be updated
        contact.FullName.Should().Be("Alicia Marie Brown");
    }

    [Fact]
    public async Task FullName_CanBeFilteredOn()
    {
        // Arrange: Create contacts with different names
        var uniqueId = Guid.NewGuid().ToString();
        var contact1Dto = TestData.Generate<TestContact>(uniqueId + "_1");
        contact1Dto.FirstName = "TestFilter";
        contact1Dto.LastName = "UniqueLastName";

        var contact2Dto = TestData.Generate<TestContact>(uniqueId + "_2");
        contact2Dto.FirstName = "AnotherTest";
        contact2Dto.LastName = "DifferentName";

        await PostTest<Contact>(ContactsUrl, contact1Dto);
        await PostTest<Contact>(ContactsUrl, contact2Dto);

        // Act: Filter by full name
        var result = await GetTest<List<Contact>>($"{ContactsUrl}?filter[where][fullName]=TestFilter UniqueLastName");

        // Assert
        result.Should().NotBeNull();
        result!.Should().Contain(c => c.FirstName == "TestFilter" && c.LastName == "UniqueLastName");
        result!.Should().NotContain(c => c.FirstName == "AnotherTest");
    }

    [Fact]
    public async Task FullName_CanBeOrderedOn()
    {
        // Arrange: Create contacts with different names
        var uniqueId = Guid.NewGuid().ToString();
        var contact1Dto = TestData.Generate<TestContact>(uniqueId + "_1");
        contact1Dto.FirstName = "Zoe";
        contact1Dto.LastName = "Anderson";

        var contact2Dto = TestData.Generate<TestContact>(uniqueId + "_2");
        contact2Dto.FirstName = "Alice";
        contact2Dto.LastName = "Baker";

        await PostTest<Contact>(ContactsUrl, contact1Dto);
        await PostTest<Contact>(ContactsUrl, contact2Dto);

        // Act: Order by full name ascending
        var result = await GetTest<List<Contact>>($"{ContactsUrl}?filter[order]=fullName&filter[limit]=10");

        // Assert
        result.Should().NotBeNull();
        var contacts = result!;
        var testContacts = contacts.Where(c => c.Email == contact1Dto.Email || c.Email == contact2Dto.Email).ToList();
        testContacts.Count.Should().Be(2);

        // Alice Baker should come before Zoe Anderson
        var idx1 = contacts.FindIndex(c => c.Email == contact1Dto.Email);
        var idx2 = contacts.FindIndex(c => c.Email == contact2Dto.Email);
        idx2.Should().BeLessThan(idx1, "Alice Baker should come before Zoe Anderson");
    }

    [Fact]
    public async Task FullName_CanUseLikeOperator()
    {
        // Arrange: Create contact
        var uniqueId = Guid.NewGuid().ToString()[..8];
        var contactDto = TestData.Generate<TestContact>(uniqueId);
        contactDto.FirstName = "Christopher";
        contactDto.MiddleName = "James";
        contactDto.LastName = "Martinez";

        await PostTest<Contact>(ContactsUrl, contactDto);

        // Act: Search using like operator
        var result = await GetTest<List<Contact>>($"{ContactsUrl}?filter[where][fullName][like]=Christopher.*");

        // Assert
        result.Should().NotBeNull();
        result!.Should().Contain(c => c.Email == contactDto.Email);
    }
}
