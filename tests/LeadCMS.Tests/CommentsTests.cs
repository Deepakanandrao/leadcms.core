// <copyright file="CommentsTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;

namespace LeadCMS.Tests;

public class CommentsTests : TableWithFKTests<Comment, TestComment, CommentUpdateDto, ICommentService>
{
    public CommentsTests()
        : base("/api/comments")
    {
        TrackEntityType<Content>();
        TrackEntityType<Contact>();
    }

    [Fact]
    public async Task GetAllTestAnonymous()
    {
        await GetAllRecords(true);
    }

    [Fact]
    public async Task GetCommentsWithStatistics()
    {
        // Arrange - create some test data with different statuses
        await CreateFKItemsWithUid();

        // Create a comment that will be NotApproved by default (anonymous user)
        var testComment1 = new TestComment(string.Empty, 1);
        var comment1Url = await PostTest(itemsUrl, testComment1);
        var comment1 = await GetTest<Comment>(comment1Url);

        // Create an approved comment (authenticated user would set status to Approved, simulating by direct DB)
        var testComment2 = new TestComment(string.Empty, 1);
        var comment2Url = await PostTest(itemsUrl, testComment2);
        var comment2 = await GetTest<Comment>(comment2Url);

        // Create a spam comment
        var testComment3 = new TestComment(string.Empty, 1);
        var comment3Url = await PostTest(itemsUrl, testComment3);
        var comment3 = await GetTest<Comment>(comment3Url);

        // Update statuses using a single DbContext instance
        var dbContext = App.GetDbContext()!;
        var dbComment1 = dbContext.Comments!.Find(comment1!.Id);
        dbComment1!.Status = CommentStatus.NotApproved;

        var dbComment2 = dbContext.Comments!.Find(comment2!.Id);
        dbComment2!.Status = CommentStatus.Approved;

        var dbComment3 = dbContext.Comments!.Find(comment3!.Id);
        dbComment3!.Status = CommentStatus.Spam;

        await dbContext.SaveChangesAsync();

        // Act - call the GetWithStatistics endpoint
        var result = await GetTest<CommentsWithStatisticsDto>($"{itemsUrl}/with-statistics");

        // Assert - check that the response structure is correct
        result.Should().NotBeNull();
        result!.Comments.Should().NotBeNull();
        result.Statistics.Should().NotBeNull();

        // Verify statistics counts using dictionary
        result.Statistics["NotApproved"].Should().Be(1);
        result.Statistics["Approved"].Should().Be(1);
        result.Statistics["Spam"].Should().Be(1);

        // Verify that comments are included in the response
        result.Comments.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetCommentsWithStatisticsAnonymous()
    {
        // Arrange - create some test data with different statuses
        await CreateFKItemsWithUid();

        // Create a comment that will be NotApproved by default (anonymous user)
        var testComment1 = new TestComment(string.Empty, 1);
        var comment1Url = await PostTest(itemsUrl, testComment1);
        var comment1 = await GetTest<Comment>(comment1Url);

        // Create an approved comment
        var testComment2 = new TestComment(string.Empty, 1);
        var comment2Url = await PostTest(itemsUrl, testComment2);
        var comment2 = await GetTest<Comment>(comment2Url);

        // Update statuses using a single DbContext instance
        var dbContext = App.GetDbContext()!;
        var dbComment1 = dbContext.Comments!.Find(comment1!.Id);
        dbComment1!.Status = CommentStatus.NotApproved;

        var dbComment2 = dbContext.Comments!.Find(comment2!.Id);
        dbComment2!.Status = CommentStatus.Approved;

        await dbContext.SaveChangesAsync();

        // Act - call the GetWithStatistics endpoint as anonymous user
        Logout();
        var result = await GetTest<AnonymousCommentsWithStatisticsDto>($"{itemsUrl}/with-statistics");

        // Assert - check that the anonymous response structure is correct
        result.Should().NotBeNull();
        result!.Comments.Should().NotBeNull();
        result.Statistics.Should().NotBeNull();

        // Verify statistics counts using dictionary
        result.Statistics["NotApproved"].Should().Be(1);
        result.Statistics["Approved"].Should().Be(1);
        result.Statistics["Spam"].Should().Be(0);

        // Verify that comments are included in the response (anonymous version)
        result.Comments.Should().HaveCount(2);

        // Verify that sensitive data is not exposed in anonymous response
        result.Comments.Should().AllSatisfy(c =>
        {
            c.Should().BeOfType<AnonymousCommentDetailsDto>();
            // Anonymous comments should not have email or contact info exposed
        });
    }

    [Fact]
    public async Task GetCommentsWithStatisticsRespectFilters()
    {
        // Arrange - create test data with different statuses and content
        await CreateFKItemsWithUid();

        // Create comments for different commentable entities
        var testComment1 = new TestComment("test1", 1) { Body = "First comment" };
        var comment1Url = await PostTest(itemsUrl, testComment1);
        var comment1 = await GetTest<Comment>(comment1Url);

        var testComment2 = new TestComment("test2", 2) { Body = "Second comment" };
        var comment2Url = await PostTest(itemsUrl, testComment2);
        var comment2 = await GetTest<Comment>(comment2Url);

        var testComment3 = new TestComment("test3", 1) { Body = "Third comment" };
        var comment3Url = await PostTest(itemsUrl, testComment3);
        var comment3 = await GetTest<Comment>(comment3Url);

        // Update statuses using a single DbContext instance
        var dbContext = App.GetDbContext()!;
        var dbComment1 = dbContext.Comments!.Find(comment1!.Id);
        dbComment1!.Status = CommentStatus.NotApproved;

        var dbComment2 = dbContext.Comments!.Find(comment2!.Id);
        dbComment2!.Status = CommentStatus.Approved;

        var dbComment3 = dbContext.Comments!.Find(comment3!.Id);
        dbComment3!.Status = CommentStatus.Spam;

        await dbContext.SaveChangesAsync();

        // Act - call with filter for specific commentable ID
        var filteredResult = await GetTest<CommentsWithStatisticsDto>($"{itemsUrl}/with-statistics?filter[where][commentableId]=1");

        // Assert - should only include statistics for comments with commentableId=1
        filteredResult.Should().NotBeNull();
        filteredResult!.Statistics["NotApproved"].Should().Be(1); // Only comment1
        filteredResult.Statistics["Approved"].Should().Be(0);     // comment2 has commentableId=2
        filteredResult.Statistics["Spam"].Should().Be(1);         // Only comment3

        // Comments should also be filtered
        filteredResult.Comments.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCommentsWithStatisticsIgnoresStatusFilter()
    {
        // Arrange - create test data with different statuses
        await CreateFKItemsWithUid();

        // Create comments with different statuses
        var testComment1 = new TestComment(string.Empty, 1);
        var comment1Url = await PostTest(itemsUrl, testComment1);
        var comment1 = await GetTest<Comment>(comment1Url);

        var testComment2 = new TestComment(string.Empty, 1);
        var comment2Url = await PostTest(itemsUrl, testComment2);
        var comment2 = await GetTest<Comment>(comment2Url);

        var testComment3 = new TestComment(string.Empty, 1);
        var comment3Url = await PostTest(itemsUrl, testComment3);
        var comment3 = await GetTest<Comment>(comment3Url);

        // Update statuses using a single DbContext instance
        var dbContext = App.GetDbContext()!;
        var dbComment1 = dbContext.Comments!.Find(comment1!.Id);
        dbComment1!.Status = CommentStatus.NotApproved;

        var dbComment2 = dbContext.Comments!.Find(comment2!.Id);
        dbComment2!.Status = CommentStatus.Approved;

        var dbComment3 = dbContext.Comments!.Find(comment3!.Id);
        dbComment3!.Status = CommentStatus.Spam;

        await dbContext.SaveChangesAsync();

        // Act - call with status filter (should only return approved comments, but stats should show all)
        var filteredResult = await GetTest<CommentsWithStatisticsDto>($"{itemsUrl}/with-statistics?filter[where][status]=Approved");

        // Assert - comments should be filtered to only approved
        filteredResult.Should().NotBeNull();
        filteredResult!.Comments.Should().HaveCount(1);
        filteredResult.Comments.Should().AllSatisfy(c => c.Id.Should().Be(comment2.Id));

        // But statistics should show counts for ALL statuses (ignoring the status filter)
        filteredResult.Statistics["NotApproved"].Should().Be(1); // comment1
        filteredResult.Statistics["Approved"].Should().Be(1);    // comment2
        filteredResult.Statistics["Spam"].Should().Be(1);        // comment3
    }

    [Fact]
    public async Task CreateAndGetItemTestAnonymous()
    {
        await CreateAndGetItem(true);
    }

    [Fact]
    public override async Task UpdateItemNotFoundTest()
    {
        var comment = new CommentUpdateDto();
        comment.Body = "Content";
        await PatchTest(itemsUrlNotFound, comment, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("commentsBasic.csv", 2)]
    [InlineData("commentsBasic.json", 2)]
    public async Task ImportFileAddUpdateBasicTest(string fileName, int expectedCount)
    {
        await CreateFKItemsWithUid();
        await CreateItem();

        await PostImportTest(itemsUrl, fileName);

        var newComment = await GetTest<Comment>($"{itemsUrl}/2");
        newComment.Should().NotBeNull();

        var allCommentsResponse = await GetTest(itemsUrl);
        allCommentsResponse.Should().NotBeNull();

        var content = await allCommentsResponse.Content.ReadAsStringAsync();
        var allComments = JsonSerializer.Deserialize<List<Comment>>(content);
        allComments.Should().NotBeNull();
        allComments!.Count.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData("commentsBasic.csv")]
    [InlineData("commentsBasic.json")]
    public async Task ImportFileAddUpdateDataTest(string fileName)
    {
        await CreateFKItemsWithUid();
        await CreateItem();

        await PostImportTest(itemsUrl, fileName);

        var updatedComment = await GetTest<Comment>($"{itemsUrl}/1");
        updatedComment.Should().NotBeNull();

        updatedComment!.CommentableId.Should().Be(1);
        updatedComment!.ContactId.Should().NotBe(0);
        updatedComment!.AuthorName.Should().Be("Author Name 1");

        var newComment = await GetTest<Comment>($"{itemsUrl}/2");
        newComment.Should().NotBeNull();

        newComment!.CommentableId.Should().Be(1);
        newComment!.ContactId.Should().NotBe(0);
        newComment!.AuthorName.Should().Be("Author Name 2");
        newComment!.CreatedAt.Should().Be(DateTime.Parse("2023-01-15T17:32:02.074179Z").ToUniversalTime());
    }

    [Theory]
    [InlineData("commentsFull.csv")]
    [InlineData("commentsFull.json")]
    public async Task ImportFileAddUpdateAllFieldsTest(string fileName)
    {
        await CreateFKItemsWithUid();
        await CreateItem();

        await PostImportTest(itemsUrl, fileName);

        var updatedComment = App.GetDbContext()!.Comments!.First(c => c.Id == 1);
        updatedComment.Should().NotBeNull();

        updatedComment!.CommentableId.Should().Be(1);
        updatedComment!.ContactId.Should().NotBe(0);
        updatedComment!.AuthorName.Should().Be("Author Name 1");
        updatedComment!.UpdatedAt.Should().Be(DateTime.Parse("2023-01-15T17:32:02.074179Z").ToUniversalTime());
        updatedComment!.CreatedAt.Should().Be(DateTime.Parse("2023-01-15T17:32:02.074179Z").ToUniversalTime());
        updatedComment!.CreatedByIp.Should().Be("192.168.1.1");
        updatedComment!.UpdatedByIp.Should().Be("192.168.1.3");
        updatedComment!.CreatedByUserAgent.Should().Be("TestAgent1");
        updatedComment!.UpdatedByUserAgent.Should().Be("TestAgent3");

        var newComment = App.GetDbContext()!.Comments!.First(c => c.Id == 2);
        newComment.Should().NotBeNull();

        newComment!.CommentableId.Should().Be(1);
        newComment!.ContactId.Should().NotBe(0);
        newComment!.AuthorName.Should().Be("Author Name 2");
        newComment!.CreatedAt.Should().Be(DateTime.Parse("2023-01-15T17:32:02.074179Z").ToUniversalTime());
        newComment!.UpdatedAt.Should().BeNull();
        newComment!.CreatedByIp.Should().Be("192.168.1.2");
        newComment!.CreatedByUserAgent.Should().Be("TestAgent2");
    }

    [Fact]
    public async Task ImportFileWithParentUniqueKeyTest()
    {
        await CreateFKItemsWithUid();
        await PostImportTest(itemsUrl, "commentsNoFKHasUKey.csv");

        var addedComment1 = App.GetDbContext()!.Comments!.First(c => c.Id == 1);
        addedComment1.Should().NotBeNull();
        addedComment1.CommentableId.Should().Be(1);

        var addedComment2 = App.GetDbContext()!.Comments!.First(c => c.Id == 2);
        addedComment2.Should().NotBeNull();
        addedComment2.CommentableId.Should().Be(2);

        await PostImportTest(itemsUrl, "commentsNoFKHasUKeyUpdate.csv");
        var updatedComment = App.GetDbContext()!.Comments!.First(c => c.Id == 1);
        updatedComment.Should().NotBeNull();
        updatedComment.CommentableId.Should().Be(2);
    }

    [Fact]
    public async Task ImportFileWithParentKeysTest()
    {
        await CreateFKItemsWithUid(6);
        await PostImportTest(itemsUrl, "commentsWithParentKey.csv");

        var addedComment1 = App.GetDbContext()!.Comments!.First(c => c.Id == 1);
        addedComment1.Should().NotBeNull();

        var addedComment2 = App.GetDbContext()!.Comments!.First(c => c.Id == 2);
        addedComment2.Should().NotBeNull();
        addedComment2.ParentId.Should().Be(1);

        var addedComment3 = App.GetDbContext()!.Comments!.First(c => c.Id == 3);
        addedComment3.Should().NotBeNull();
        addedComment3.ParentId.Should().Be(1);

        await PostImportTest(itemsUrl, "commentsWithOldParentKey.csv");

        var addedComment4 = App.GetDbContext()!.Comments!.First(c => c.Id == 4);
        addedComment4.Should().NotBeNull();
        addedComment4.ParentId.Should().Be(1);

        var addedComment5 = App.GetDbContext()!.Comments!.First(c => c.Id == 5);
        addedComment5.Should().NotBeNull();
        addedComment5.ParentId.Should().Be(2);

        var addedComment6 = App.GetDbContext()!.Comments!.First(c => c.Id == 6);
        addedComment6.Should().NotBeNull();
        addedComment6.ParentId.Should().Be(4);
    }

    [Fact]
    public async Task MultiIterationsImportTest()
    {
        await CreateFKItemsWithUid(1);

        var importResult = await PostImportTest(itemsUrl, "commentsWithKey.csv");

        importResult.Added.Should().Be(4);
        importResult.Updated.Should().Be(0);
        importResult.Failed.Should().Be(0);
        importResult.Skipped.Should().Be(0);

        importResult = await PostImportTest(itemsUrl, "commentsWithKeyUpdate.csv");

        importResult.Added.Should().Be(2);
        importResult.Updated.Should().Be(1);
        importResult.Failed.Should().Be(0);
        importResult.Skipped.Should().Be(3);

        importResult = await PostImportTest(itemsUrl, "commentsWithKeyUpdate.csv");

        importResult.Added.Should().Be(1);
        importResult.Updated.Should().Be(0);
        importResult.Failed.Should().Be(0);
        importResult.Skipped.Should().Be(5);

        importResult = await PostImportTest(itemsUrl, "commentsWithKeyUpdateWithErrors.csv");

        importResult.Added.Should().Be(1);
        importResult.Updated.Should().Be(1);
        importResult.Failed.Should().Be(2);
        importResult.Skipped.Should().Be(2);
    }

    [Fact(Skip = "Comment doesn't contain real fk items yet")]
    public override Task CascadeDeleteTest()
    {
        throw new NotImplementedException();
    }

    [Fact]
    public override async Task CreateItemWithNonExistedFKItemTest()
    {
        var testItem = TestData.Generate<TestComment>(string.Empty, 0);
        await PostTest(itemsUrl, testItem, HttpStatusCode.NotFound);
    }

    protected override void MustBeEquivalent(object? expected, object? result)
    {
        result.Should().BeEquivalentTo(expected, options => options
            .Excluding(o => ((TestComment)o!).ContactId)
            .Excluding(o => ((TestComment)o!).CommentableUid));

        ((Comment)result!).ContactId.Should().BePositive();
    }

    protected override async Task<(TestComment, string)> CreateItem(string uid, int fkId)
    {
        var testComment = new TestComment(uid, fkId);

        var newCommentUrl = await PostTest(itemsUrl, testComment);

        return (testComment, newCommentUrl);
    }

    protected override async Task<(int, string)> CreateFKItem()
    {
        var fkItemCreate = new TestContent(Guid.NewGuid().ToString("N")[..8]);

        var fkUrl = await PostTest("/api/content", fkItemCreate);

        var fkItem = await GetTest<Content>(fkUrl);

        fkItem.Should().NotBeNull();

        return (fkItem!.Id, fkUrl);
    }

    protected override CommentUpdateDto UpdateItem(TestComment to)
    {
        var from = new CommentUpdateDto();
        to.Body = from.Body = to.Body + "Updated";
        return from;
    }

    private async Task CreateFKItemsWithUid(int contactsCount = 2)
    {
        var fkItemCreate1 = new TestContent("100");
        var fkItemCreate2 = new TestContent("101");

        await PostTest("/api/content", fkItemCreate1);
        await PostTest("/api/content", fkItemCreate2);

        for (var i = 1; i <= contactsCount; i++)
        {
            var contact = new TestContact(i.ToString());
            await PostTest("/api/contacts", contact);
        }
    }
}