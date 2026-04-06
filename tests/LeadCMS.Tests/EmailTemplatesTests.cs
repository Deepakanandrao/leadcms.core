// <copyright file="EmailTemplatesTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DataAnnotations;

namespace LeadCMS.Tests;

public class EmailTemplatesTests : TableWithFKTests<EmailTemplate, TestEmailTemplate, EmailTemplateUpdateDto, IEntityService<EmailTemplate>>
{
    public EmailTemplatesTests()
        : base("/api/email-templates")
    {
    }

    [Fact]
    public async Task GetWithWhereContainsLanguageTest()
    {
        // EmailTemplate does not have [SupportsElastic], so it always uses the DBQueryProvider
        Attribute.GetCustomAttribute(typeof(EmailTemplate), typeof(SupportsElasticAttribute)).Should().BeNull();

        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var bulkEntitiesList = new List<EmailTemplate>();

        var bulkList = TestData.GenerateAndPopulateAttributes<TestEmailTemplate>("1", tc => tc.Language = "ru", fkId);
        bulkEntitiesList.Add(mapper.Map<EmailTemplate>(bulkList));
        bulkList = TestData.GenerateAndPopulateAttributes<TestEmailTemplate>("2", tc => tc.Language = "russian", fkId);
        bulkEntitiesList.Add(mapper.Map<EmailTemplate>(bulkList));
        bulkList = TestData.GenerateAndPopulateAttributes<TestEmailTemplate>("3", tc => tc.Language = "ru-RU", fkId);
        bulkEntitiesList.Add(mapper.Map<EmailTemplate>(bulkList));
        bulkList = TestData.GenerateAndPopulateAttributes<TestEmailTemplate>("4", tc => tc.Language = "en", fkId);
        bulkEntitiesList.Add(mapper.Map<EmailTemplate>(bulkList));
        bulkList = TestData.GenerateAndPopulateAttributes<TestEmailTemplate>("5", tc => tc.Language = "de", fkId);
        bulkEntitiesList.Add(mapper.Map<EmailTemplate>(bulkList));

        PopulateBulkData<EmailTemplate, IEntityService<EmailTemplate>>(bulkEntitiesList);

        // Exact match only - no results because pattern without wildcards requires full string match
        var result = await GetTest<List<EmailTemplate>>(itemsUrl + "?filter[where][Language][contains]=ru");
        result!.Count.Should().Be(1);

        // Prefix match: should return "ru", "russian", "ru-RU" (3 items)
        result = await GetTest<List<EmailTemplate>>(itemsUrl + "?filter[where][Language][contains]=ru*");
        result!.Count.Should().Be(3);

        // Suffix match: "ru" ends with "ru", and "ru-RU" ends with "RU" (case-insensitive "ru"), so 2 match
        result = await GetTest<List<EmailTemplate>>(itemsUrl + "?filter[where][Language][contains]=*ru");
        result!.Count.Should().Be(2);

        // Substring match
        result = await GetTest<List<EmailTemplate>>(itemsUrl + "?filter[where][Language][contains]=*u*");
        result!.Count.Should().Be(3);

        // Test full query with ordering by nested emailGroup.name (mirrors the reported failing URL)
        var detailsResult = await GetTest<List<EmailTemplateDetailsDto>>(itemsUrl + "?filter[limit]=10&filter[order]=emailGroup.name asc&filter[skip]=0&filter[include]=EmailGroup&filter[where][Language][contains]=ru*");
        detailsResult!.Count.Should().Be(3);
        detailsResult.TrueForAll(t => t.EmailGroup != null).Should().BeTrue();
    }

    [Fact]
    public async Task GetSenderNames_ReturnsDistinctValues()
    {
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var uid1 = Guid.NewGuid().ToString("N")[..8];
        var uid2 = Guid.NewGuid().ToString("N")[..8];

        var template1 = new TestEmailTemplate(uid1, fkId) { FromName = "Sales Team", Language = "en" };
        var template2 = new TestEmailTemplate(uid2, fkId) { FromName = "Support Team", Language = "en" };

        await PostTest(itemsUrl, template1, HttpStatusCode.Created);
        await PostTest(itemsUrl, template2, HttpStatusCode.Created);

        var senderNames = await GetTest<string[]>($"{itemsUrl}/sender-names", HttpStatusCode.OK);
        senderNames.Should().NotBeNull();
        senderNames.Should().Contain("Sales Team");
        senderNames.Should().Contain("Support Team");
    }

    [Fact]
    public async Task GetSenderNames_FilteredByLanguage()
    {
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var uid1 = Guid.NewGuid().ToString("N")[..8];
        var uid2 = Guid.NewGuid().ToString("N")[..8];

        var template1 = new TestEmailTemplate(uid1, fkId) { FromName = "English Sender", Language = "en" };
        var template2 = new TestEmailTemplate(uid2, fkId) { FromName = "French Sender", Language = "fr" };

        await PostTest(itemsUrl, template1, HttpStatusCode.Created);
        await PostTest(itemsUrl, template2, HttpStatusCode.Created);

        var senderNames = await GetTest<string[]>($"{itemsUrl}/sender-names?language=fr", HttpStatusCode.OK);
        senderNames.Should().NotBeNull();
        senderNames.Should().Contain("French Sender");
        senderNames.Should().NotContain("English Sender");
    }

    [Fact]
    public async Task GetSenderEmails_ReturnsDistinctValues()
    {
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var uid1 = Guid.NewGuid().ToString("N")[..8];
        var uid2 = Guid.NewGuid().ToString("N")[..8];

        var template1 = new TestEmailTemplate(uid1, fkId) { FromEmail = "sales@example.com", Language = "en" };
        var template2 = new TestEmailTemplate(uid2, fkId) { FromEmail = "support@example.com", Language = "en" };

        await PostTest(itemsUrl, template1, HttpStatusCode.Created);
        await PostTest(itemsUrl, template2, HttpStatusCode.Created);

        var senderEmails = await GetTest<string[]>($"{itemsUrl}/sender-emails", HttpStatusCode.OK);
        senderEmails.Should().NotBeNull();
        senderEmails.Should().Contain("sales@example.com");
        senderEmails.Should().Contain("support@example.com");
    }

    [Fact]
    public async Task GetSenderEmails_FilteredByLanguage()
    {
        var fkItem = await CreateFKItem();
        var fkId = fkItem.Item1;

        var uid1 = Guid.NewGuid().ToString("N")[..8];
        var uid2 = Guid.NewGuid().ToString("N")[..8];

        var template1 = new TestEmailTemplate(uid1, fkId) { FromEmail = "en@example.com", Language = "en" };
        var template2 = new TestEmailTemplate(uid2, fkId) { FromEmail = "de@example.com", Language = "de" };

        await PostTest(itemsUrl, template1, HttpStatusCode.Created);
        await PostTest(itemsUrl, template2, HttpStatusCode.Created);

        var senderEmails = await GetTest<string[]>($"{itemsUrl}/sender-emails?language=de", HttpStatusCode.OK);
        senderEmails.Should().NotBeNull();
        senderEmails.Should().Contain("de@example.com");
        senderEmails.Should().NotContain("en@example.com");
    }

    protected override async Task<(TestEmailTemplate, string)> CreateItem(string uid, int fkId)
    {
        var emailTemplate = new TestEmailTemplate(uid, fkId);

        var emailTemplateUrl = await PostTest(itemsUrl, emailTemplate);

        return (emailTemplate, emailTemplateUrl);
    }

    protected override async Task<(int, string)> CreateFKItem()
    {
        var fkItemCreate = new TestEmailGroup(Guid.NewGuid().ToString("N")[..8]);

        var fkUrl = await PostTest("/api/email-groups", fkItemCreate, HttpStatusCode.Created);

        var fkItem = await GetTest<EmailGroup>(fkUrl);

        fkItem.Should().NotBeNull();

        return (fkItem!.Id, fkUrl);
    }

    protected override EmailTemplateUpdateDto UpdateItem(TestEmailTemplate to)
    {
        var from = new EmailTemplateUpdateDto();
        to.Name = from.Name = to.Name + "Updated";
        return from;
    }
}