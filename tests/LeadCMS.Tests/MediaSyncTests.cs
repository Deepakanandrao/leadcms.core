// <copyright file="MediaSyncTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net.Http.Headers;
using System.Net.Http.Json;
using LeadCMS.DTOs;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.StaticFiles;

namespace LeadCMS.Tests;

public class MediaSyncTests : BaseTestAutoLogin
{
    public MediaSyncTests()
    {
        TrackEntityType<Media>();
        TrackEntityType<ChangeLog>();
    }

    [Fact]
    public async Task Sync_InitialSync_ShouldReturnAllMedia()
    {
        // Arrange: upload two media files
        var media1 = await UploadMediaAsync("sync-initial-1.png", "sync-initial");
        var media2 = await UploadMediaAsync("sync-initial-2.png", "sync-initial");

        // Act: initial sync (no token)
        var syncResult = await GetSyncResult("/api/media/sync");

        // Assert: both items returned
        syncResult.Should().NotBeNull();
        syncResult!.Items.Should().NotBeNull();
        syncResult.Items!.Count.Should().BeGreaterThanOrEqualTo(2);
        syncResult.Items.Should().Contain(i => i.Id == media1.Id);
        syncResult.Items.Should().Contain(i => i.Id == media2.Id);
        syncResult.Deleted.Should().BeNullOrEmpty();
        syncResult.NextSyncToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Sync_AfterDelete_ShouldReturnDeletedPath()
    {
        // Arrange: upload a media file and get initial sync token
        await UploadMediaAsync("sync-delete-1.png", "sync-delete");
        var initialSync = await GetSyncResult("/api/media/sync");
        var syncToken = initialSync!.NextSyncToken;
        syncToken.Should().NotBeNullOrWhiteSpace();

        // Act: delete the media file
        await DeleteTest($"/api/media/sync-delete/sync-delete-1.png");

        // Sync with the token
        var syncResult = await GetSyncResult($"/api/media/sync?syncToken={syncToken}");

        // Assert: deleted path (scopeUid + name) should appear in the deleted list
        syncResult.Should().NotBeNull();
        syncResult!.Deleted.Should().NotBeNull();
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-delete" && d.Name == "sync-delete-1.png");
    }

    [Fact]
    public async Task Sync_AfterBulkDelete_ShouldReturnAllDeletedPaths()
    {
        // Arrange: upload multiple media files and get initial sync token
        var media1 = await UploadMediaAsync("sync-bulk-del-1.png", "sync-bulk-del");
        var media2 = await UploadMediaAsync("sync-bulk-del-2.png", "sync-bulk-del");
        await UploadMediaAsync("sync-bulk-del-3.png", "sync-bulk-del");
        var initialSync = await GetSyncResult("/api/media/sync");
        var syncToken = initialSync!.NextSyncToken;

        // Act: bulk delete first two files
        var deletePayload = new List<int> { media1.Id, media2.Id };
        await DeleteTest("/api/media/bulk", deletePayload);

        // Sync with the token
        var syncResult = await GetSyncResult($"/api/media/sync?syncToken={syncToken}");

        // Assert: deleted paths for the two deleted files should appear
        syncResult.Should().NotBeNull();
        syncResult!.Deleted.Should().NotBeNull();
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-bulk-del" && d.Name == "sync-bulk-del-1.png");
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-bulk-del" && d.Name == "sync-bulk-del-2.png");
        // media3 should NOT be in deleted
        syncResult.Deleted.Should().NotContain(d => d.Name == "sync-bulk-del-3.png");
    }

    [Fact]
    public async Task Sync_AfterFolderDelete_ShouldReturnAllDeletedPaths()
    {
        // Arrange: upload media files in a folder
        await UploadMediaAsync("sync-folder-del-1.png", "sync-folder-del/sub");
        await UploadMediaAsync("sync-folder-del-2.png", "sync-folder-del/sub");
        await UploadMediaAsync("sync-folder-keep.png", "sync-folder-keep");
        var initialSync = await GetSyncResult("/api/media/sync");
        var syncToken = initialSync!.NextSyncToken;

        // Act: delete folder
        var deleteRequest = new MediaBulkDeleteRequestDto { Folder = "sync-folder-del" };
        await PostTest<MediaOptimizeResponseDto>("/api/media/delete-folder", deleteRequest, HttpStatusCode.OK);

        // Sync with the token
        var syncResult = await GetSyncResult($"/api/media/sync?syncToken={syncToken}");

        // Assert: all media paths in the folder should be reported as deleted
        syncResult.Should().NotBeNull();
        syncResult!.Deleted.Should().NotBeNull();
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-folder-del/sub" && d.Name == "sync-folder-del-1.png");
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-folder-del/sub" && d.Name == "sync-folder-del-2.png");
        // media outside the folder should NOT appear in deleted
        syncResult.Deleted.Should().NotContain(d => d.ScopeUid == "sync-folder-keep");
    }

    [Fact]
    public async Task Sync_AfterRename_ShouldReturnRenamedItemAndOldPathAsDeleted()
    {
        // Arrange: upload a file and get sync token
        var media = await UploadMediaAsync("sync-rename-1.png", "sync-rename");
        var initialSync = await GetSyncResult("/api/media/sync");
        var syncToken = initialSync!.NextSyncToken;

        // Act: rename the file
        var renameRequest = new MediaRenameRequestDto
        {
            ScopeUid = "sync-rename",
            FileName = "sync-rename-1.png",
            NewScopeUid = "sync-rename-new",
            NewFileName = "sync-renamed.png",
        };
        await PostTest<MediaDetailsDto>("/api/media/rename", renameRequest, HttpStatusCode.OK);

        // Sync with the token
        var syncResult = await GetSyncResult($"/api/media/sync?syncToken={syncToken}");

        // Assert: the renamed file should appear in items with new name/scope
        syncResult.Should().NotBeNull();
        syncResult!.Items.Should().NotBeNull();
        var renamedItem = syncResult.Items!.FirstOrDefault(i => i.Id == media.Id);
        renamedItem.Should().NotBeNull();
        renamedItem!.ScopeUid.Should().Be("sync-rename-new");
        renamedItem.Name.Should().Be("sync-renamed.png");

        // The old file path should appear in deleted so the client can remove the old local file
        syncResult.Deleted.Should().NotBeNull();
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-rename" && d.Name == "sync-rename-1.png");
    }

    [Fact]
    public async Task Sync_AfterFolderRename_ShouldReturnRenamedItemsAndOldPathsAsDeleted()
    {
        // Arrange: upload files in a folder
        var media1 = await UploadMediaAsync("sync-frename-1.png", "sync-frename/sub");
        var media2 = await UploadMediaAsync("sync-frename-2.png", "sync-frename/sub");
        var initialSync = await GetSyncResult("/api/media/sync");
        var syncToken = initialSync!.NextSyncToken;

        // Act: rename folder
        var renameRequest = new MediaBulkRenameRequestDto
        {
            Folder = "sync-frename",
            NewFolder = "sync-frename-new",
        };
        await PostTest<MediaOptimizeResponseDto>("/api/media/rename-folder", renameRequest, HttpStatusCode.OK);

        // Sync with the token
        var syncResult = await GetSyncResult($"/api/media/sync?syncToken={syncToken}");

        // Assert: renamed files should appear in items with new scope
        syncResult.Should().NotBeNull();
        syncResult!.Items.Should().NotBeNull();

        var item1 = syncResult.Items!.FirstOrDefault(i => i.Id == media1.Id);
        item1.Should().NotBeNull();
        item1!.ScopeUid.Should().Be("sync-frename-new/sub");

        var item2 = syncResult.Items!.FirstOrDefault(i => i.Id == media2.Id);
        item2.Should().NotBeNull();
        item2!.ScopeUid.Should().Be("sync-frename-new/sub");

        // Old paths should appear in deleted
        syncResult.Deleted.Should().NotBeNull();
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-frename/sub" && d.Name == "sync-frename-1.png");
        syncResult.Deleted.Should().Contain(d => d.ScopeUid == "sync-frename/sub" && d.Name == "sync-frename-2.png");
    }

    [Fact]
    public async Task Sync_NoChangesSinceLastSync_ShouldReturnNoContent()
    {
        // Arrange: upload a file and sync
        await UploadMediaAsync("sync-nochange-1.png", "sync-nochange");
        var initialSync = await GetSyncResult("/api/media/sync");
        var syncToken = initialSync!.NextSyncToken;

        // Act: sync again without any changes
        var response = await GetRequest($"/api/media/sync?syncToken={syncToken}");

        // Assert: should return 204 No Content
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Sync_NewFileAfterSync_ShouldReturnOnlyNewFile()
    {
        // Arrange: upload initial file and sync
        var media1 = await UploadMediaAsync("sync-new-1.png", "sync-new");
        var initialSync = await GetSyncResult("/api/media/sync");
        var syncToken = initialSync!.NextSyncToken;

        // Act: upload a new file
        var media2 = await UploadMediaAsync("sync-new-2.png", "sync-new");

        // Sync with the token
        var syncResult = await GetSyncResult($"/api/media/sync?syncToken={syncToken}");

        // Assert: only the new file should appear
        syncResult.Should().NotBeNull();
        syncResult!.Items.Should().NotBeNull();
        syncResult.Items.Should().Contain(i => i.Id == media2.Id);
        syncResult.Items.Should().NotContain(i => i.Id == media1.Id);
    }

    private async Task<MediaDetailsDto> UploadMediaAsync(string fileName, string scopeUid)
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.TryGetContentType(fileName, out var contentType);

        var bytes = new byte[1024];

        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream(bytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        form.Add(fileContent, "File", fileName);
        form.Add(new StringContent(scopeUid), "ScopeUid");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/media")
        {
            Content = form,
        };
        request.Headers.Authorization = GetAuthenticationHeaderValue();

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var media = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        media.Should().NotBeNull();

        return media!;
    }

    private async Task<MediaSyncResult?> GetSyncResult(string url)
    {
        var response = await GetRequest(url);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            string? token = null;
            if (response.Headers.TryGetValues(ResponseHeaderNames.NextSyncToken, out var tokenValues))
            {
                token = tokenValues.FirstOrDefault();
            }

            return new MediaSyncResult
            {
                Response = new SyncResponseDto<MediaDetailsDto, MediaDeletedDto>(),
                NextSyncToken = token,
            };
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var syncResponse = JsonHelper.Deserialize<SyncResponseDto<MediaDetailsDto, MediaDeletedDto>>(content);

        var result = new MediaSyncResult { Response = syncResponse! };

        if (response.Headers.TryGetValues(ResponseHeaderNames.NextSyncToken, out var values))
        {
            result.NextSyncToken = values.FirstOrDefault();
        }

        return result;
    }

    private class MediaSyncResult
    {
        public SyncResponseDto<MediaDetailsDto, MediaDeletedDto> Response { get; set; } = new();

        public List<MediaDetailsDto> Items => Response.Items;

        public List<MediaDeletedDto> Deleted => Response.Deleted;

        public string? NextSyncToken { get; set; }
    }
}
