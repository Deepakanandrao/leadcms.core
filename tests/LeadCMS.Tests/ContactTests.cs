// <copyright file="ContactTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using LeadCMS.Infrastructure;

namespace LeadCMS.Tests;

public class ContactTests : SimpleTableTests<Contact, TestContact, ContactUpdateDto, IContactService>
{
    public ContactTests()
        : base("/api/contacts")
    {
    }

    [Fact]
    public async Task ContactAccountTaskTest()
    {
        var notinitializedEmail = "email@notinitializeddomain.com";

        // posted contacts marked as notInitialized and the details of their accounts should be retrieved
        var item = TestData.Generate<TestContact>();
        item.Email = notinitializedEmail;
        await PostTest(itemsUrl, item);

        // imported contacts marked as notIntended and the details of their accounts shouldn't be retrieved
        await PostImportTest(itemsUrl, "contacts.json");

        var executeResponce = await GetRequest("/api/tasks/execute/ContactAccountTask");
        executeResponce.StatusCode.Should().Be(HttpStatusCode.OK);

        var contacts = App.GetDbContext()!.Contacts!.ToList();
        contacts.Count.Should().BeGreaterThan(1);

        foreach (var contact in contacts)
        {
            var domain = App.GetDbContext()!.Domains!.FirstOrDefault(d => d.Id == contact!.DomainId);
            domain.Should().NotBeNull();
            if (contact.Email == notinitializedEmail)
            {
                domain!.AccountStatus.Should().Be(AccountSyncStatus.Successful);
                domain.AccountId.Should().NotBeNull();
            }
            else
            {
                domain!.AccountStatus.Should().Be(AccountSyncStatus.NotIntended);
                domain.AccountId.Should().BeNull();
            }

            domain.AccountId.Should().Be(contact.AccountId);
        }
    }

    [Fact]
    public async Task PatchContactAccountIdTest()
    {
        // Create a contact
        var item = TestData.Generate<TestContact>();
        var createUrl = await PostTest(itemsUrl, item);
        createUrl.Should().NotBeNull();

        // Create two accounts to test switching between them
        var dbContext = App.GetDbContext()!;
        var account1 = new Account { Name = "Account 1" };
        var account2 = new Account { Name = "Account 2" };
        dbContext.Accounts!.AddRange(account1, account2);
        await dbContext.SaveChangesAsync();

        // Patch contact with first account
        var update1 = new ContactUpdateDto { AccountId = account1.Id };
        var response1 = await Patch(createUrl, update1);
        if (response1.StatusCode != HttpStatusCode.OK)
        {
            var error1 = await response1.Content.ReadAsStringAsync();
            throw new Exception($"PATCH failed with {response1.StatusCode}: {error1}");
        }

        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        var contact1 = await GetTest<ContactDetailsDto>(createUrl);
        contact1.Should().NotBeNull();
        contact1!.AccountId.Should().Be(account1.Id);

        // Patch contact with second account
        var update2 = new ContactUpdateDto { AccountId = account2.Id };
        var response2 = await PatchTest(createUrl, update2);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var contact2 = await GetTest<ContactDetailsDto>(createUrl);
        contact2.Should().NotBeNull();
        contact2!.AccountId.Should().Be(account2.Id);

        // Clear the account by setting it to null using raw JSON
        // We need to use raw JSON to ensure "accountId": null is sent
        var json3 = "{\"accountId\": null}";
        var content3 = new System.Net.Http.StringContent(json3, System.Text.Encoding.UTF8, "application/json");
        var request3 = new HttpRequestMessage(HttpMethod.Patch, createUrl) { Content = content3 };
        request3.Headers.Authorization = GetAuthenticationHeaderValue();
        var response3 = await client.SendAsync(request3);
        response3.StatusCode.Should().Be(HttpStatusCode.OK);

        var contact3 = await GetTest<ContactDetailsDto>(createUrl);
        contact3.Should().NotBeNull();
        contact3!.AccountId.Should().BeNull();
    }

    [Fact]

    public async Task GetWithSearchEmailTest()
    {
        var item = TestData.Generate<TestContact>();
        var firstPart = "abcd";
        var secondPart = "gmail.com";
        item.Email = $"{firstPart}@{secondPart}";
        item.LastName = "Some last name";
        await PostTest(itemsUrl, item);

        await SyncElasticSearch();

        var result = await GetTest<List<Contact>>(itemsUrl + $"?query={firstPart}");
        result.Should().NotBeNull();
        result!.Count.Should().Be(0);

        result = await GetTest<List<Contact>>(itemsUrl + $"?query={secondPart}");
        result.Should().NotBeNull();
        result!.Count.Should().Be(0);

        result = await GetTest<List<Contact>>(itemsUrl + $"?query={item.Email}");
        result.Should().NotBeNull();
        result!.Count.Should().Be(1);

        result = await GetTest<List<Contact>>(itemsUrl + "?query=Some");
        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
    }

    [Fact]
    public async Task CheckInsertedItemDomain()
    {
        var testCreateItem = await CreateItem();

        var returnedDomain = DomainChecker(testCreateItem.Item1.Email!);
        returnedDomain.Should().NotBeNull();
    }

    [Theory]
    [InlineData("contacts.json")]
    public async Task ImportFileAddCheckDomain(string fileName)
    {
        await PostImportTest(itemsUrl, fileName);

        var newContact = await GetTest<Contact>($"{itemsUrl}/2");
        newContact.Should().NotBeNull();

        var returnedDomain = DomainChecker(newContact!.Email!);
        returnedDomain.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportFileUpdateByIndexTest()
    {
        await PostImportTest(itemsUrl, "contactBase.csv");
        var allContactsResponse = await GetTest(itemsUrl);
        allContactsResponse.Should().NotBeNull();

        var content = await allContactsResponse.Content.ReadAsStringAsync();
        var allContacts = JsonSerializer.Deserialize<List<Contact>>(content);
        allContacts.Should().NotBeNull();
        allContacts!.Count.Should().Be(4);

        await PostImportTest(itemsUrl, "contactsToUpdate.csv");
        var contact1 = App.GetDbContext()!.Contacts!.First(c => c.Id == 1);
        contact1.Should().NotBeNull();
        // contact1 updated by Id
        contact1.LastName.Should().Be("adam_parker");

        var contact2 = App.GetDbContext()!.Contacts!.First(c => c.Id == 2);
        contact2.Should().NotBeNull();
        // contact2 no id provided, updated by Email
        contact2.LastName.Should().Be("garry_bolt");

        var contact3 = App.GetDbContext()!.Contacts!.First(c => c.Id == 3);
        contact3.Should().NotBeNull();
        // contact3 no id provided, updated by Email
        contact3.LastName.Should().Be("Siri_Tom");
    }

    [Fact]
    public async Task ParentDeletionRestrictWithChildren()
    {
        var contactId = 0;

        var testCreateItem = await CreateItem();

        contactId = Convert.ToInt32(testCreateItem.Item2.Split("/").Last());

        var dbContext = App.GetDbContext();
        var dbDomainId = dbContext!.Contacts!.Where(contactsDb => contactsDb.Id == contactId).Select(contact => contact.DomainId).FirstOrDefault();

        await DeleteTest($"/api/domains/{dbDomainId}");
    }

    [Fact]
    public async Task DeleteContact_ShouldSetEmailLogContactIdToNull()
    {
        TrackEntityType<EmailLog>();

        var testCreateItem = await CreateItem();
        var contactId = Convert.ToInt32(testCreateItem.Item2.Split("/").Last());

        var dbContext = App.GetDbContext()!;
        var contact = await dbContext.Contacts!.FindAsync(contactId);
        contact.Should().NotBeNull();

        var emailLog = new EmailLog
        {
            ContactId = contactId,
            Subject = "Delete behavior test",
            Recipients = contact!.Email!,
            FromEmail = "sender@test.net",
            TextBody = "Body",
            MessageId = "delete-contact-email-log",
            Status = EmailStatus.Sent,
            CreatedAt = DateTime.UtcNow,
        };

        await dbContext.EmailLogs!.AddAsync(emailLog);
        await dbContext.SaveChangesAsync();

        await DeleteTest($"/api/contacts/{contactId}");

        dbContext = App.GetDbContext()!;
        var deletedContact = await dbContext.Contacts!.FindAsync(contactId);
        deletedContact.Should().BeNull();

        var persistedEmailLog = await dbContext.EmailLogs!.FindAsync(emailLog.Id);
        persistedEmailLog.Should().NotBeNull();
        persistedEmailLog!.ContactId.Should().BeNull();
    }

    [Fact]
    public async Task GetOneWithIncludeAccountAndDomain()
    {
        TrackEntityType<Account>();
        TrackEntityType<Domain>();

        // Create a contact (which auto-creates a domain)
        var item = TestData.Generate<TestContact>();
        var createUrl = await PostTest(itemsUrl, item);
        createUrl.Should().NotBeNull();

        var contactId = Convert.ToInt32(createUrl.Split("/").Last());

        // Create an account and link it to the contact
        var dbContext = App.GetDbContext()!;
        var account = new Account { Name = "IncludeTestAccount_" + Guid.NewGuid().ToString("N")[..8] };
        dbContext.Accounts!.Add(account);
        await dbContext.SaveChangesAsync();

        var contact = dbContext.Contacts!.First(c => c.Id == contactId);
        contact.AccountId = account.Id;
        await dbContext.SaveChangesAsync();

        // GET by ID without includes — navigation properties should be null
        var resultWithoutIncludes = await GetTest<ContactDetailsDto>($"{itemsUrl}/{contactId}");
        resultWithoutIncludes.Should().NotBeNull();
        resultWithoutIncludes!.AccountId.Should().Be(account.Id);
        resultWithoutIncludes.DomainId.Should().BeGreaterThan(0);
        resultWithoutIncludes.Account.Should().BeNull();
        resultWithoutIncludes.Domain.Should().BeNull();

        // GET by ID with filter[include]=Account&filter[include]=Domain
        var resultWithIncludes = await GetTest<ContactDetailsDto>($"{itemsUrl}/{contactId}?filter[include]=Account&filter[include]=Domain");
        resultWithIncludes.Should().NotBeNull();
        resultWithIncludes!.AccountId.Should().Be(account.Id);
        resultWithIncludes.DomainId.Should().BeGreaterThan(0);
        resultWithIncludes.Account.Should().NotBeNull();
        resultWithIncludes.Account!.Id.Should().Be(account.Id);
        resultWithIncludes.Domain.Should().NotBeNull();
        resultWithIncludes.Domain!.Id.Should().Be(contact.DomainId);

        // Second-level navigation properties should be cleaned up (null)
        resultWithIncludes.Account.Contacts.Should().BeNull();
        resultWithIncludes.Account.Domains.Should().BeNull();
        resultWithIncludes.Domain.Account.Should().BeNull();
        resultWithIncludes.Domain.Contacts.Should().BeNull();
    }

    [Fact]
    public async Task DeleteContact_ShouldSetUnsubscribeContactIdToNull()
    {
        TrackEntityType<Unsubscribe>();

        var testCreateItem = await CreateItem();
        var contactId = Convert.ToInt32(testCreateItem.Item2.Split("/").Last());

        var dbContext = App.GetDbContext()!;

        var unsubscribe = new Unsubscribe
        {
            ContactId = contactId,
            Reason = "Delete behavior test",
            CreatedAt = DateTime.UtcNow,
            Source = "ContactTests",
        };

        await dbContext.Unsubscribes!.AddAsync(unsubscribe);
        await dbContext.SaveChangesAsync();

        await DeleteTest($"/api/contacts/{contactId}");

        dbContext = App.GetDbContext()!;
        var deletedContact = await dbContext.Contacts!.FindAsync(contactId);
        deletedContact.Should().BeNull();

        var persistedUnsubscribe = await dbContext.Unsubscribes!.FindAsync(unsubscribe.Id);
        persistedUnsubscribe.Should().NotBeNull();
        persistedUnsubscribe!.ContactId.Should().BeNull();
    }

    [Fact]
    public async Task DuplicatedRecordsImportTest()
    {
        // first attempt to import records with some duplicates
        var importResult = await PostImportTest(itemsUrl, "contactsWithDuplicates.csv");

        importResult.Added.Should().Be(2);
        importResult.Updated.Should().Be(0);
        importResult.Failed.Should().Be(2);
        importResult.Skipped.Should().Be(0);

        importResult.Errors!.Count.Should().Be(2);

        // second attempt to import records with some duplicates
        importResult = await PostImportTest(itemsUrl, "contactsWithDuplicatesUpdate.csv");

        importResult.Added.Should().Be(0);
        importResult.Updated.Should().Be(2);
        importResult.Failed.Should().Be(2);
        importResult.Skipped.Should().Be(0);

        importResult.Errors!.Count.Should().Be(2);

        // third attempt to import records with some duplicates
        importResult = await PostImportTest(itemsUrl, "contactsWithDuplicatesUpdate.csv");

        importResult.Added.Should().Be(0);
        importResult.Updated.Should().Be(0);
        importResult.Failed.Should().Be(2);
        importResult.Skipped.Should().Be(2);

        importResult.Errors!.Count.Should().Be(2);
    }

    [Theory]
    [InlineData("filter[where][id]=9", HttpStatusCode.OK)]
    [InlineData("filter[where][id][eq]=9", HttpStatusCode.OK)]
    [InlineData("filter[order]=id", HttpStatusCode.OK)]
    [InlineData("filter[skip]=5", HttpStatusCode.OK)]
    [InlineData("filter[limit]=5", HttpStatusCode.OK)]
    public async Task ValidQueryParameter(string filter, HttpStatusCode code)
    {
        await GetTest($"{itemsUrl}?{filter}", code);
    }

    [Theory]
    [InlineData("filtercadabra", HttpStatusCode.OK)] // No '=' sign, silently ignored
    [InlineData("filter", HttpStatusCode.OK)] // No '=' sign, silently ignored
    [InlineData("filter[]", HttpStatusCode.OK)] // No '=' sign, silently ignored
    [InlineData("filter[]=", HttpStatusCode.BadRequest)]
    [InlineData("filter[]=0", HttpStatusCode.BadRequest)]
    [InlineData("filter[][]=3", HttpStatusCode.BadRequest)]
    [InlineData("filter[][][]=4", HttpStatusCode.BadRequest)]
    [InlineData("filter[notexists]=5", HttpStatusCode.BadRequest)]
    [InlineData("filter[where][notexists]=6", HttpStatusCode.BadRequest)]
    [InlineData("filter[where][][]=7", HttpStatusCode.BadRequest)]
    [InlineData("filter[where][id][]=8", HttpStatusCode.BadRequest)]
    [InlineData("filter[where][id][notexists]=9", HttpStatusCode.BadRequest)]
    [InlineData("filter[^7@5\\nwhere][id^7@5\\n][|^7@5\\n]=^7@5\\n", HttpStatusCode.BadRequest)]
    [InlineData("filter[where][id]=^7@5\\n", HttpStatusCode.BadRequest)]
    [InlineData("filter[][id]=^7@5\\n", HttpStatusCode.BadRequest)]
    [InlineData("filter[where][id][eq]=^7@5\\n", HttpStatusCode.BadRequest)]
    [InlineData("filter[limit]=abc", HttpStatusCode.BadRequest)]
    [InlineData("filter[skip]=abc", HttpStatusCode.BadRequest)]
    [InlineData("filter[order]=5555incorrectfield777", HttpStatusCode.BadRequest)]
    public async Task InvalidQueryParameter(string filter, HttpStatusCode code)
    {
        await GetTest($"{itemsUrl}?{filter}", code);
    }

    [Theory]
    [InlineData(true, "", 1, 1)]
    [InlineData(true, "filter[where][id][eq]=1", 1, 1)]
    [InlineData(true, "filter[where][id][eq]=100", 0, 0)]
    [InlineData(true, "filter[limit]=10&filter[skip]=0", 1, 1)]
    [InlineData(true, "filter[limit]=10&filter[skip]=100", 1, 0)]
    [InlineData(false, "", 0, 0)]
    [InlineData(false, "filter[where][id][eq]=1", 0, 0)]
    public async Task GetTotalCountTest(bool createTestItem, string filter, int totalCount, int payloadItemsCount)
    {
        if (createTestItem)
        {
            await CreateItem();
        }

        var response = await GetTest($"{itemsUrl}?{filter}");
        response.Should().NotBeNull();

        var totalCountHeader = response.Headers.GetValues(ResponseHeaderNames.TotalCount).FirstOrDefault();
        totalCountHeader.Should().Be($"{totalCount}");
        var content = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<List<Contact>>(content);
        payload.Should().NotBeNull();
        payload.Should().HaveCount(payloadItemsCount);
    }

    [Theory]
    [InlineData("", 15, 10)]
    [InlineData("filter[skip]=0", 15, 10)]
    [InlineData("filter[limit]=10&filter[skip]=0", 15, 10)]
    public async Task LimitLists(string filter, int dataCount, int limitPerRequest)
    {
        GenerateBulkRecords(dataCount);

        var response = await GetTest($"{itemsUrl}?{filter}");
        response.Should().NotBeNull();

        var json = await response.Content.ReadAsStringAsync();

        var deserialized = JsonSerializer.Deserialize<List<Contact>>(json!);

        var returendCount = deserialized!.Count!;

        Assert.True(returendCount <= limitPerRequest);
    }

    [Theory]
    [InlineData("filter[limit]=15001", 15)]
    public async Task InvalidLimit(string filter, int dataCount)
    {
        GenerateBulkRecords(dataCount);

        await GetTest($"{itemsUrl}?{filter}", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePhoneOnlyContact_ShouldSucceed_WithNoDomain()
    {
        var phoneOnlyContact = new ContactCreateDto
        {
            Phone = "+14155559999",
            FirstName = "PhoneOnly",
            Language = "en",
        };

        var createUrl = await PostTest(itemsUrl, phoneOnlyContact);
        createUrl.Should().NotBeNull();

        var contactId = Convert.ToInt32(createUrl.Split("/").Last());
        var dbContext = App.GetDbContext()!;
        var contact = await dbContext.Contacts!.FindAsync(contactId);

        contact.Should().NotBeNull();
        contact!.Email.Should().BeNull();
        contact.Phone.Should().Be("+14155559999");
        contact.DomainId.Should().BeNull();
    }

    [Fact]
    public async Task CreatePhoneOnlyContact_ThenAddEmail_ShouldCreateDomain()
    {
        TrackEntityType<Domain>();

        var phoneOnlyContact = new ContactCreateDto
        {
            Phone = "+14155558888",
            FirstName = "PhoneThenEmail",
            Language = "en",
        };

        var createUrl = await PostTest(itemsUrl, phoneOnlyContact);
        createUrl.Should().NotBeNull();

        var contactId = Convert.ToInt32(createUrl.Split("/").Last());
        var dbContext = App.GetDbContext()!;
        var contact = await dbContext.Contacts!.FindAsync(contactId);
        contact!.DomainId.Should().BeNull();

        // Now patch to add email
        var update = new ContactUpdateDto { Email = "phonethenemail@example.com" };
        var patchResponse = await Patch(createUrl, update);
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        dbContext = App.GetDbContext()!;
        var updatedContact = await dbContext.Contacts!.FindAsync(contactId);
        updatedContact!.Email.Should().Be("phonethenemail@example.com");
        updatedContact.DomainId.Should().NotBeNull("adding email should create and link a domain");
    }

    [Fact]
    public async Task ImportMixedContacts_ShouldHandlePhoneOnlyEntries()
    {
        var result = await PostImportTest(itemsUrl, "contacts_mixed.json");

        result.Should().NotBeNull();
        result.Added.Should().Be(2);

        var dbContext = App.GetDbContext()!;
        var contacts = dbContext.Contacts!.ToList();

        // Phone-only contact — no domain
        var phoneOnly = contacts.FirstOrDefault(c => c.FirstName == "PhoneOnly");
        phoneOnly.Should().NotBeNull();
        phoneOnly!.Email.Should().BeNull();
        phoneOnly.Phone.Should().Be("+14155551000");
        phoneOnly.DomainId.Should().BeNull();

        // Mixed contact — has domain
        var mixed = contacts.FirstOrDefault(c => c.FirstName == "Mixed");
        mixed.Should().NotBeNull();
        mixed!.Email.Should().Be("mixed@example.com");
        mixed.DomainId.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateContactWithoutEmailOrPhone_ShouldSucceed()
    {
        var bareContact = new ContactCreateDto
        {
            FirstName = "BareMinimum",
            Language = "en",
        };

        var createUrl = await PostTest(itemsUrl, bareContact);
        createUrl.Should().NotBeNull();

        var contactId = Convert.ToInt32(createUrl.Split("/").Last());
        var dbContext = App.GetDbContext()!;
        var contact = await dbContext.Contacts!.FindAsync(contactId);

        contact.Should().NotBeNull();
        contact!.Email.Should().BeNull();
        contact.Phone.Should().BeNull();
        contact.DomainId.Should().BeNull();
    }

    protected override ContactUpdateDto UpdateItem(TestContact to)
    {
        var from = new ContactUpdateDto();
        var updatedEmail = "updated" + to.Email;
        to.Email = updatedEmail;
        from.Email = updatedEmail;
        return from;
    }

    protected override void GenerateBulkRecords(int dataCount, Action<TestContact>? populateAttributes = null)
    {
        var contacts = new List<Contact>();

        for (var i = 0; i < dataCount; i++)
        {
            var contact = new Contact();
            contact.Email = $"contact{i}@test{i}.net";
            contact.Domain = new Domain() { Name = contact.Email.Split("@").Last().ToLower() };
            contacts.Add(contact);
        }

        // Track both Contact and Domain since we're creating both
        TrackEntityType<Domain>();
        PopulateBulkData<Contact, IContactService>(contacts);
    }

    private string DomainChecker(string email)
    {
        var domain = email.Split("@").Last().ToString();
        var dbContext = App.GetDbContext();
        var dbDomain = dbContext!.Domains!.Where(domainDb => domainDb.Name == domain).Select(domainDb => domainDb.Name).FirstOrDefault();

        return dbDomain!;
    }
}
