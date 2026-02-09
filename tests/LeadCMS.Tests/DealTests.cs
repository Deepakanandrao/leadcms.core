// <copyright file="DealTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Authentication;
using LeadCMS.Helpers;
using LeadCMS.Tests.TestEntities;

namespace LeadCMS.Tests;

public class DealTests : SimpleTableTests<Deal, TestDeal, DealUpdateDto, IDealService>
{
    private readonly List<string> createdUserIds = new List<string>();

    public DealTests()
        : base("/api/deals")
    {
        TrackEntityType<Contact>();
        TrackEntityType<Account>();
        TrackEntityType<DealPipeline>();
        TrackEntityType<DealPipelineStage>();
    }

    public override void Dispose()
    {
        // Delete test users created during this test
        if (createdUserIds.Any())
        {
            var dbContext = App.GetDbContext()!;
            var usersToDelete = dbContext.Users!.Where(u => createdUserIds.Contains(u.Id)).ToList();
            dbContext.Users!.RemoveRange(usersToDelete);
            dbContext.SaveChanges();
        }

        base.Dispose();
    }

    [Fact]
    public async Task CreateAndUpdateItemTestWithContacts()
    {
        // successful creation
        var testContacts = new List<TestContact>() { new TestContact("1"), new TestContact("2") };
        var fkData = await CreateFKItems(testContacts);
        var dealCreate = new TestDeal(string.Empty, fkData.ContactIds, fkData.AccountId, fkData.PipelineId, fkData.UserId);
        var url = await PostTest(itemsUrl, dealCreate);
        var items = await GetTest<List<DealDetailsDto>>("/api/deals?filter[include]=Contacts", HttpStatusCode.OK);
        items!.Count.Should().Be(1);
        items[0].Contacts!.Select(c => c.Email).Should().BeEquivalentTo(testContacts.Select(tc => tc.Email));
        var existedContactsIds = items[0].Contacts!.Select(c => c.Id).ToList();

        // failed creation
        dealCreate = new TestDeal(string.Empty, new HashSet<int>() { existedContactsIds.Max() + 1 }, fkData.AccountId, fkData.PipelineId, fkData.UserId);
        await PostTest(itemsUrl, dealCreate, HttpStatusCode.NotFound);

        // successful patching
        fkData.ContactIds.Remove(fkData.ContactIds.First());
        var dealUpdate = new DealUpdateDto() { ContactIds = fkData.ContactIds };
        await PatchTest(url, dealUpdate);
        items = await GetTest<List<DealDetailsDto>>("/api/deals?filter[include]=Contacts", HttpStatusCode.OK);
        items!.Count.Should().Be(1);
        items[0].Contacts!.Select(c => c.Id).Should().BeEquivalentTo(fkData.ContactIds);

        // failed patching
        dealUpdate = new DealUpdateDto() { ContactIds = new HashSet<int>() { existedContactsIds.Max() + 1 } };
        await PatchTest(url, dealUpdate, HttpStatusCode.NotFound);
    }

    protected override async Task<(TestDeal, string)> CreateItem()
    {
        var fkData = await CreateFKItems(new List<TestContact>());

        var dealCreate = new TestDeal(Guid.NewGuid().ToString("N")[..8], fkData.ContactIds, fkData.AccountId, fkData.PipelineId, fkData.UserId);
        var dealUrl = await PostTest(itemsUrl, dealCreate);

        return (dealCreate, dealUrl);
    }

    protected override void GenerateBulkRecords(int dataCount, Action<TestDeal>? populateAttributes = null)
    {
        var fkData = CreateFKItems(new List<TestContact>()).Result;

        var bulkList = TestData.GenerateAndPopulateAttributes<TestDeal>(dataCount, populateAttributes, fkData.ContactIds, fkData.AccountId, fkData.PipelineId, fkData.UserId);
        var bulkEntitiesList = mapper.Map<List<Deal>>(bulkList);

        PopulateBulkData<Deal, IDealService>(bulkEntitiesList);
    }

    protected override DealUpdateDto UpdateItem(TestDeal td)
    {
        var from = new DealUpdateDto();
        if (td.ExpectedCloseDate.HasValue)
        {
            td.ExpectedCloseDate = from.ExpectedCloseDate = td.ExpectedCloseDate.Value.AddDays(1);
        }
        else
        {
            td.ExpectedCloseDate = from.ExpectedCloseDate = DateTime.UtcNow;
        }

        return from;
    }

    protected override void MustBeEquivalent(object? expected, object? result)
    {
        result.Should().BeEquivalentTo(expected, options => options.Excluding(o => ((TestDeal)o!).ContactIds));
        var resultDeal = (Deal)result!;
        if (resultDeal.Contacts != null)
        {
            var expectedContactdOds = ((TestDeal)expected!).ContactIds;
            var resultContactdIds = resultDeal.Contacts!.Select(c => c.Id).ToHashSet();
            resultContactdIds.Should().BeEquivalentTo(expectedContactdOds);
        }
    }

    private async Task<FKData> CreateFKItems(List<TestContact> testContacts)
    {
        var result = new FKData();
        var uid = Guid.NewGuid().ToString("N")[..8];

        var accountCreate = new TestAccount(uid);
        var account = await PostTest<Account>("/api/accounts", accountCreate);
        account.Should().NotBeNull();
        result.AccountId = account!.Id;

        var pipelineCreate = new TestDealPipeline(uid);
        var pipeline = await PostTest<DealPipeline>("/api/deal-pipelines", pipelineCreate);
        pipeline.Should().NotBeNull();
        result.PipelineId = pipeline!.Id;

        var userCreate = new TestUser(uid);
        var user = await PostTest<User>("/api/users", userCreate);
        user.Should().NotBeNull();
        result.UserId = user!.Id;
        // Track the created user for cleanup
        createdUserIds.Add(user.Id);

        var stages = new TestPipelineStage[] { new TestPipelineStage(uid + "0", pipeline!.Id) { Order = 0 }, new TestPipelineStage(uid + "1", pipeline!.Id) { Order = 1 } };
        foreach (var stage in stages)
        {
            var newStage = await PostTest<DealPipelineStage>("/api/deal-pipeline-stages", stage);
            newStage.Should().NotBeNull();
        }

        foreach (var contact in testContacts)
        {
            var newContact = await PostTest<Contact>("/api/contacts", contact);
            result.ContactIds.Add(newContact!.Id);
        }

        return result;
    }

    private sealed class FKData
    {
        public int AccountId { get; set; }

        public int PipelineId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public HashSet<int> ContactIds { get; set; } = new HashSet<int>();
    }
}