// <copyright file="ContentSyncTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using LeadCMS.Infrastructure;

namespace LeadCMS.Tests;

public class ContentSyncTests : BaseTestAutoLogin
{
    private const string SyncUrl = "/api/content/sync";

    public ContentSyncTests()
    {
        TrackEntityType<Content>();
        TrackEntityType<ChangeLog>();
    }

    [Fact]
    public async Task Sync_InitialSync_ShouldReturnAllContent()
    {
        // Arrange: create content items
        var content1 = await CreateContentAsync("sync-init-1");
        var content2 = await CreateContentAsync("sync-init-2");

        // Act: initial sync (no token)
        var syncResult = await GetSyncResult(SyncUrl);

        // Assert: both items returned, no base items
        syncResult.Should().NotBeNull();
        syncResult!.Items.Should().NotBeNull();
        syncResult.Items!.Count.Should().BeGreaterThanOrEqualTo(2);
        syncResult.Items.Should().Contain(i => i.Id == content1.Id);
        syncResult.Items.Should().Contain(i => i.Id == content2.Id);
        syncResult.Deleted.Should().BeNullOrEmpty();
        syncResult.NextSyncToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Sync_WithoutIncludeBase_ShouldNotReturnBaseItems()
    {
        // Arrange: create a content item, sync, then modify it
        var content = await CreateContentAsync("sync-nobase");
        var initialSync = await GetSyncResult(SyncUrl);
        var syncToken = initialSync!.NextSyncToken;

        // Modify the content
        await PatchTest($"/api/content/{content.Id}", new ContentUpdateDto { Title = "Updated Title" });

        // Act: delta sync WITHOUT includeBase
        var deltaSync = await GetSyncResult($"{SyncUrl}?syncToken={syncToken}");

        // Assert: should have the updated item but no base items
        deltaSync.Should().NotBeNull();
        deltaSync!.Items.Should().Contain(i => i.Id == content.Id);
        deltaSync.Response.BaseItems.Should().BeNull();
    }

    [Fact]
    public async Task Sync_WithIncludeBase_ShouldReturnBaseVersionOfModifiedItems()
    {
        // Arrange: create a content item and get the initial sync token
        var content = await CreateContentAsync("sync-base");
        var initialSync = await GetSyncResult(SyncUrl);
        var syncToken = initialSync!.NextSyncToken;
        syncToken.Should().NotBeNullOrWhiteSpace();

        var originalTitle = content.Title;

        // Modify the content
        var newTitle = "Updated Title After Sync";
        await PatchTest($"/api/content/{content.Id}", new ContentUpdateDto { Title = newTitle });

        // Act: delta sync WITH includeBase=true
        var deltaSync = await GetSyncResult($"{SyncUrl}?syncToken={syncToken}&includeBase=true");

        // Assert: should have the updated item AND its base version
        deltaSync.Should().NotBeNull();
        deltaSync!.Items.Should().NotBeNullOrEmpty();

        var currentItem = deltaSync.Items!.FirstOrDefault(i => i.Id == content.Id);
        currentItem.Should().NotBeNull();
        currentItem!.Title.Should().Be(newTitle);

        deltaSync.Response.BaseItems.Should().NotBeNull();
        deltaSync.Response.BaseItems.Should().ContainKey(content.Id);

        var baseItem = deltaSync.Response.BaseItems![content.Id];
        baseItem.Title.Should().Be(originalTitle);
    }

    [Fact]
    public async Task Sync_WithIncludeBase_NewlyCreatedItems_ShouldNotHaveBaseVersion()
    {
        // Arrange: get sync token before creating content
        var initialSync = await GetSyncResult(SyncUrl);
        var syncToken = initialSync!.NextSyncToken;

        // Create content AFTER getting the sync token
        var content = await CreateContentAsync("sync-new-nobase");

        // Act: delta sync with includeBase=true
        var deltaSync = await GetSyncResult($"{SyncUrl}?syncToken={syncToken}&includeBase=true");

        // Assert: the new item should be in Items but NOT in BaseItems (it didn't exist before)
        deltaSync.Should().NotBeNull();
        deltaSync!.Items.Should().Contain(i => i.Id == content.Id);

        // BaseItems should either be null or not contain the newly created item
        if (deltaSync.Response.BaseItems != null)
        {
            deltaSync.Response.BaseItems.Should().NotContainKey(content.Id);
        }
    }

    [Fact]
    public async Task Sync_WithIncludeBase_MultipleEdits_ShouldReturnVersionAtSyncTokenTime()
    {
        // Arrange: create content, sync, make two edits
        var content = await CreateContentAsync("sync-multi-edit");
        var originalTitle = content.Title;
        var originalBody = content.Body;

        var initialSync = await GetSyncResult(SyncUrl);
        var syncToken = initialSync!.NextSyncToken;

        // First edit: change title
        await PatchTest($"/api/content/{content.Id}", new ContentUpdateDto { Title = "First Edit" });

        // Second edit: change body
        await PatchTest($"/api/content/{content.Id}", new ContentUpdateDto { Body = "Second Edit Body" });

        // Act: delta sync with includeBase
        var deltaSync = await GetSyncResult($"{SyncUrl}?syncToken={syncToken}&includeBase=true");

        // Assert: current item should reflect latest state
        var currentItem = deltaSync!.Items!.FirstOrDefault(i => i.Id == content.Id);
        currentItem.Should().NotBeNull();
        currentItem!.Title.Should().Be("First Edit");
        currentItem.Body.Should().Be("Second Edit Body");

        // Base item should reflect the state AT the sync token time (original)
        deltaSync.Response.BaseItems.Should().NotBeNull();
        deltaSync.Response.BaseItems.Should().ContainKey(content.Id);

        var baseItem = deltaSync.Response.BaseItems![content.Id];
        baseItem.Title.Should().Be(originalTitle);
        baseItem.Body.Should().Be(originalBody);
    }

    [Fact]
    public async Task Sync_WithIncludeBase_DeletedItems_ShouldNotHaveBaseVersion()
    {
        // Arrange: create content, sync, then delete it
        var content = await CreateContentAsync("sync-delete");
        var initialSync = await GetSyncResult(SyncUrl);
        var syncToken = initialSync!.NextSyncToken;

        await DeleteTest($"/api/content/{content.Id}");

        // Act: delta sync with includeBase
        var deltaSync = await GetSyncResult($"{SyncUrl}?syncToken={syncToken}&includeBase=true");

        // Assert: the deleted item should be in Deleted, not in BaseItems
        deltaSync.Should().NotBeNull();
        deltaSync!.Deleted.Should().Contain(content.Id);

        if (deltaSync.Response.BaseItems != null)
        {
            deltaSync.Response.BaseItems.Should().NotContainKey(content.Id);
        }
    }

    [Fact]
    public async Task Sync_NoChangesSinceLastSync_ShouldReturnNoContent()
    {
        // Arrange: create content and sync
        await CreateContentAsync("sync-nochange");
        var initialSync = await GetSyncResult(SyncUrl);
        var syncToken = initialSync!.NextSyncToken;

        // Act: sync again without any changes
        var response = await GetRequest($"{SyncUrl}?syncToken={syncToken}&includeBase=true");

        // Assert: should return 204 NoContent
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Sync_WithIncludeBase_MixedNewAndModified_ShouldOnlyReturnBaseForModified()
    {
        // Arrange: create one content item, get sync token
        var existingContent = await CreateContentAsync("sync-mixed-existing");
        var initialSync = await GetSyncResult(SyncUrl);
        var syncToken = initialSync!.NextSyncToken;

        // After sync token: create a new item and modify the existing one
        var newContent = await CreateContentAsync("sync-mixed-new");
        await PatchTest($"/api/content/{existingContent.Id}", new ContentUpdateDto { Title = "Modified After Sync" });

        // Act: delta sync with includeBase
        var deltaSync = await GetSyncResult($"{SyncUrl}?syncToken={syncToken}&includeBase=true");

        // Assert: both items in Items
        deltaSync.Should().NotBeNull();
        deltaSync!.Items.Should().Contain(i => i.Id == existingContent.Id);
        deltaSync.Items.Should().Contain(i => i.Id == newContent.Id);

        // BaseItems should contain a base only for the modified item, not the new one
        deltaSync.Response.BaseItems.Should().NotBeNull();
        deltaSync.Response.BaseItems.Should().ContainKey(existingContent.Id);
        deltaSync.Response.BaseItems.Should().NotContainKey(newContent.Id);
    }

    private async Task<ContentDetailsDto> CreateContentAsync(string uid = "")
    {
        // Use a GUID suffix to ensure unique slugs when tests run in parallel
        var uniqueUid = $"{uid}-{Guid.NewGuid().ToString("N")[..8]}";
        var testContent = new TestContent(uniqueUid);
        var result = await PostTest<ContentDetailsDto>("/api/content", testContent, HttpStatusCode.Created);
        result.Should().NotBeNull();
        return result!;
    }

    private async Task<ContentSyncResult?> GetSyncResult(string url)
    {
        var response = await GetRequest(url);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            string? token = null;
            if (response.Headers.TryGetValues(ResponseHeaderNames.NextSyncToken, out var tokenValues))
            {
                token = tokenValues.FirstOrDefault();
            }

            return new ContentSyncResult
            {
                Response = new SyncResponseDto<ContentDetailsDto, int>(),
                NextSyncToken = token,
            };
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var syncResponse = JsonHelper.Deserialize<SyncResponseDto<ContentDetailsDto, int>>(content);

        var result = new ContentSyncResult { Response = syncResponse! };

        if (response.Headers.TryGetValues(ResponseHeaderNames.NextSyncToken, out var values))
        {
            result.NextSyncToken = values.FirstOrDefault();
        }

        return result;
    }

    private class ContentSyncResult
    {
        public SyncResponseDto<ContentDetailsDto, int> Response { get; set; } = new();

        public List<ContentDetailsDto> Items => Response.Items;

        public List<int> Deleted => Response.Deleted;

        public string? NextSyncToken { get; set; }
    }
}
