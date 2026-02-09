// <copyright file="BulkDeleteTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests;

public class BulkDeleteTests : BaseTestAutoLogin
{
    private const string ContactsUrl = "/api/contacts";
    private const string UsersUrl = "/api/users";

    [Fact]
    public async Task BulkDeleteContacts_ShouldRemoveAll()
    {
        TrackEntityType<Contact>();

        var item1 = TestData.Generate<TestContact>("bulk1");
        var item2 = TestData.Generate<TestContact>("bulk2");

        var location1 = await PostTest(ContactsUrl, item1);
        var location2 = await PostTest(ContactsUrl, item2);

        var id1 = int.Parse(location1.Split('/').Last());
        var id2 = int.Parse(location2.Split('/').Last());

        await DeleteTest($"{ContactsUrl}/bulk", new[] { id1, id2 });

        await GetTest($"{ContactsUrl}/{id1}", HttpStatusCode.NotFound);
        await GetTest($"{ContactsUrl}/{id2}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkDeleteContacts_WithDuplicateIds_ShouldSucceed()
    {
        TrackEntityType<Contact>();

        var item = TestData.Generate<TestContact>("bulkdup");
        var location = await PostTest(ContactsUrl, item);
        var id = int.Parse(location.Split('/').Last());

        await DeleteTest($"{ContactsUrl}/bulk", new[] { id, id, id });

        await GetTest($"{ContactsUrl}/{id}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkDeleteContacts_WithEmptyBody_ShouldReturnUnprocessableEntity()
    {
        await DeleteTest($"{ContactsUrl}/bulk", Array.Empty<int>(), HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task BulkDeleteContacts_WithNonExistentIds_ShouldReturnNotFound()
    {
        await DeleteTest($"{ContactsUrl}/bulk", new[] { 999998, 999999 }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkDeleteContacts_WithMixedExistentAndNonExistentIds_ShouldReturnNotFound()
    {
        TrackEntityType<Contact>();

        var item = TestData.Generate<TestContact>("bulkmix");
        var location = await PostTest(ContactsUrl, item);
        var existingId = int.Parse(location.Split('/').Last());

        // Should fail because 999999 doesn't exist
        await DeleteTest($"{ContactsUrl}/bulk", new[] { existingId, 999999 }, HttpStatusCode.NotFound);

        // The existing contact should still be there (transaction rolled back)
        await GetTest($"{ContactsUrl}/{existingId}", HttpStatusCode.OK);
    }

    [Fact]
    public async Task BulkDeleteUsers_ShouldRemoveAll()
    {
        var user1 = await PostTest<UserDetailsDto>(UsersUrl, new UserCreateDto
        {
            Email = $"bulk1_{Guid.NewGuid():N}@test.com",
            UserName = $"bulk1_{Guid.NewGuid():N}",
            DisplayName = "Bulk User 1",
            GeneratePassword = true,
            Language = "en",
        });

        var user2 = await PostTest<UserDetailsDto>(UsersUrl, new UserCreateDto
        {
            Email = $"bulk2_{Guid.NewGuid():N}@test.com",
            UserName = $"bulk2_{Guid.NewGuid():N}",
            DisplayName = "Bulk User 2",
            GeneratePassword = true,
            Language = "en",
        });

        user1.Should().NotBeNull();
        user2.Should().NotBeNull();

        await DeleteTest($"{UsersUrl}/bulk", new[] { user1!.Id, user2!.Id });

        await GetTest($"{UsersUrl}/{user1.Id}", HttpStatusCode.NotFound);
        await GetTest($"{UsersUrl}/{user2.Id}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BulkDeleteUsers_WithEmptyBody_ShouldReturnUnprocessableEntity()
    {
        await DeleteTest($"{UsersUrl}/bulk", Array.Empty<string>(), HttpStatusCode.UnprocessableEntity);
    }
}
