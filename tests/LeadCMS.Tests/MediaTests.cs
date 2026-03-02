// <copyright file="MediaTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LeadCMS.Constants;
using LeadCMS.Helpers;
using LeadCMS.Infrastructure;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Tests;

public class MediaTests : BaseTestAutoLogin
{
    public MediaTests()
    {
        TrackEntityType<Media>();
        TrackEntityType<Setting>();
    }

    [Theory]
    [InlineData("test1.png", 1024000, false)]
    [InlineData("test2.png", 1024, true)]
    [InlineData("test3.jpeg", 1024000, false)]
    [InlineData("test4.jpeg", 1024, true)]
    [InlineData("test5.mp4", 11000000, false)]
    [InlineData("test6.mp4", 1024, true)]
    public async Task CreateAndGetMediaTest(string fileName, int fileSize, bool shouldBePositive)
    {
        var result = await CreateAndGetMedia(fileName, fileSize);
        result.Should().Be(shouldBePositive);
    }

    [Theory]
    [InlineData("HelloWorld-ThisIs---     ...DotNet.png", "helloworld-thisis----...dotnet.png", 1024)]
    [InlineData("my_photo_file.png", "my-photo-file.png", 1024)]
    [InlineData("UPPER_CASE_File.png", "upper-case-file.png", 1024)]
    [InlineData("Стратегия цифровизации.png", "strategiia-tsifrovizatsii.png", 1024)]
    public async Task TransliterationAndSlugifyTest(string fileName, string expectedTransliteratedName, int fileSize)
    {
        var testImage = new TestMedia(fileName, fileSize);

        var postResult = await PostTest("/api/media", testImage);
        postResult.Item2.Should().BeTrue();
        var convertedFileName = Regex.Match(postResult.Item1, @"\/api\/media\/\S+\/(\S+.\S+)").Groups[1].Value;
        convertedFileName.Should().Match(expectedTransliteratedName);
        var imageStream = await GetImageTest(postResult.Item1);
        imageStream.Should().NotBeNull();
        imageStream!.Length.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("test2.png", 1024)]
    [InlineData("test4.jpeg", 1024)]
    [InlineData("test6.mp4", 1024)]
    public async Task UpdateImageTest(string fileName, int fileSize)
    {
        await CreateAndGetMedia(fileName, fileSize);
        var nonModifiedStream = await GetImageTest($"/api/media/{TestMedia.Scope}/{fileName}");

        var testImage = new TestMedia(fileName, fileSize);

        var postResult = await PostTest("/api/media", testImage);
        postResult.Item2.Should().BeTrue();
        var imageStream = await GetImageTest(postResult.Item1);
        imageStream.Should().NotBeNull();
        imageStream!.Length.Should().BeGreaterThan(0);
        if (!IsImageFile(fileName))
        {
            CompareStreams(nonModifiedStream!, imageStream!).Should().BeTrue();
            CompareStreams(testImage.DataBuffer, imageStream!).Should().BeTrue();
        }
    }

    [Fact]
    public async Task CreateImageAnonymousTest()
    {
        Logout();
        var testMedia = new TestMedia("test1.png", 1024);
        await PostTest("/api/media", testMedia, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetImageAnonymousTest()
    {
        var testMedia = new TestMedia("test1.png", 1024);
        var postResult = await PostTest("/api/media", testMedia);
        postResult.Item2.Should().BeTrue();

        Logout();
        var imageStream = await GetImageTest(postResult.Item1, HttpStatusCode.OK);
        imageStream.Should().NotBeNull();
        imageStream!.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetMedia_ByOriginalName_ShouldReturnOriginalData()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string originalFileName = "original-cover.png";
        const string scopeUid = "media-original-fallback";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        created.Should().NotBeNull();
        created!.OriginalName.Should().Be(originalFileName);
        created.Name.Should().NotBe(originalFileName);
        created.MimeType.Should().Be("image/webp");

        var response = await GetTest($"/api/media/{scopeUid}/{originalFileName}", HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");

        var downloadedBytes = await response.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(imageBytes);
    }

    [Fact]
    public async Task GetMedia_WithOriginalQuery_ShouldReturnOriginalData()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string originalFileName = "original-query-cover.png";
        const string scopeUid = "media-original-query";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        created.Should().NotBeNull();
        created!.OriginalName.Should().Be(originalFileName);
        created.Name.Should().NotBe(originalFileName);

        var optimizedResponse = await GetTest($"/api/media/{scopeUid}/{created.Name}", HttpStatusCode.OK);
        optimizedResponse.Content.Headers.ContentType?.MediaType.Should().Be("image/webp");

        var originalResponse = await GetTest($"/api/media/{scopeUid}/{created.Name}?original=true", HttpStatusCode.OK);
        originalResponse.Content.Headers.ContentType?.MediaType.Should().Be("image/png");

        var downloadedBytes = await originalResponse.Content.ReadAsByteArrayAsync();
        downloadedBytes.Should().Equal(imageBytes);
    }

    [Fact]
    public async Task UploadMedia_WithOptimisationEnabled_SetsOriginalNameAndSize()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-upload-original-metadata";
        const string originalFileName = "upload-original-metadata.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        created.Should().NotBeNull();
        created!.OriginalName.Should().Be(originalFileName);
        created.OriginalSize.Should().Be(imageBytes.LongLength);
        created.OriginalExtension.Should().Be(".png");
        created.OriginalMimeType.Should().Be("image/png");
        created.OriginalWidth.Should().Be(736);
        created.OriginalHeight.Should().Be(404);
        created.Name.Should().NotBe(originalFileName);
    }

    [Fact]
    public async Task GetMediaList_WithOptimisationEnabled_ReturnsOriginalMetadata()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-list-original-metadata";
        const string originalFileName = "list-original-metadata.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        created.Should().NotBeNull();

        var mediaList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={scopeUid}",
            HttpStatusCode.OK);

        mediaList.Should().NotBeNull();
        var media = mediaList!.Single();
        media.OriginalName.Should().Be(originalFileName);
        media.OriginalSize.Should().Be(imageBytes.LongLength);
        media.OriginalExtension.Should().Be(".png");
        media.OriginalMimeType.Should().Be("image/png");
        media.OriginalWidth.Should().Be(736);
        media.OriginalHeight.Should().Be(404);
        media.Name.Should().NotBe(originalFileName);
    }

    [Fact]
    public async Task GetMediaList_WithIncludeFolders_ReturnsOriginalMetadata()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-folder-original-metadata";
        const string originalFileName = "folder-original-metadata.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        created.Should().NotBeNull();

        var mediaList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?scopeUid={Uri.EscapeDataString(scopeUid)}&includeFolders=true&order=UsageCount DESC",
            HttpStatusCode.OK);

        mediaList.Should().NotBeNull();
        var media = mediaList!.Single(m => m.MimeType != "inode/directory");
        media.OriginalName.Should().Be(originalFileName);
        media.OriginalSize.Should().Be(imageBytes.LongLength);
        media.OriginalExtension.Should().Be(".png");
        media.OriginalMimeType.Should().Be("image/png");
        media.OriginalWidth.Should().Be(736);
        media.OriginalHeight.Should().Be(404);
        media.Name.Should().NotBe(originalFileName);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersFalse_ReturnsFlatList_WithFilters()
    {
        var scopeUid = $"media-flat-{Guid.NewGuid():N}";
        var otherScopeUid = $"media-flat-other-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "flat-a.png", scopeUid);
        await UploadMediaAsync(imageBytes, "flat-b.png", scopeUid);
        await UploadMediaAsync(imageBytes, "flat-c.png", otherScopeUid);

        var response = await GetTest(
            $"/api/media?scopeUid={Uri.EscapeDataString(scopeUid)}&includeFolders=false&filter[order]=Name ASC&filter[limit]=1",
            HttpStatusCode.OK);

        response.Headers.TryGetValues(ResponseHeaderNames.TotalCount, out var totalCountValues)
            .Should().BeTrue();
        totalCountValues!.Single().Should().Be("2");

        var items = await response.Content.ReadFromJsonAsync<List<MediaDetailsDto>>();
        items.Should().NotBeNull();
        items!.Count.Should().Be(1);
        items.TrueForAll(m => m.ScopeUid == scopeUid).Should().BeTrue();
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_ReturnsFoldersAndFiles()
    {
        var rootScopeUid = $"media-folder-{Guid.NewGuid():N}";
        var subScopeUid = $"{rootScopeUid}/sub";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "root-file.png", rootScopeUid);
        await UploadMediaAsync(imageBytes, "sub-file.png", subScopeUid);

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScopeUid)}",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Exists(i => i.MimeType == "inode/directory" && i.ScopeUid == subScopeUid).Should().BeTrue();
        items.Exists(i => i.MimeType != "inode/directory" && i.ScopeUid == rootScopeUid).Should().BeTrue();
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_AtRoot_ShowsTopLevelFolders()
    {
        var folder1 = $"root-folder1-{Guid.NewGuid():N}";
        var folder2 = $"root-folder2-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "file1.png", folder1);
        await UploadMediaAsync(imageBytes, "file2.png", folder2);
        await UploadMediaAsync(imageBytes, "file3.png", $"{folder1}/nested");

        // Query root level with includeFolders=true and no scopeUid
        var items = await GetTest<List<MediaDetailsDto>>(
            "/api/media?includeFolders=true",
            HttpStatusCode.OK);

        items.Should().NotBeNull();

        // Should have folder entries for both top-level folders
        var folderItems = items!.Where(i => i.MimeType == "inode/directory").ToList();
        folderItems.Should().Contain(f => f.ScopeUid == folder1);
        folderItems.Should().Contain(f => f.ScopeUid == folder2);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_WithNestedFolders_ShowsCorrectHierarchy()
    {
        var rootScope = $"nested-test-{Guid.NewGuid():N}";
        var level1Scope = $"{rootScope}/level1";
        var level2Scope = $"{rootScope}/level1/level2";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "root.png", rootScope);
        await UploadMediaAsync(imageBytes, "level1.png", level1Scope);
        await UploadMediaAsync(imageBytes, "level2.png", level2Scope);

        // Query at root level - should show level1 folder and root file
        var rootItems = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}",
            HttpStatusCode.OK);

        rootItems.Should().NotBeNull();
        rootItems!.Should().Contain(i => i.MimeType == "inode/directory" && i.ScopeUid == level1Scope);
        rootItems.Should().Contain(i => i.MimeType != "inode/directory" && i.Name.Contains("root"));
        rootItems.Should().NotContain(i => i.ScopeUid == level2Scope && i.MimeType != "inode/directory");

        // Query at level1 - should show level2 folder and level1 file
        var level1Items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(level1Scope)}",
            HttpStatusCode.OK);

        level1Items.Should().NotBeNull();
        level1Items!.Should().Contain(i => i.MimeType == "inode/directory" && i.ScopeUid == level2Scope);
        level1Items.Should().Contain(i => i.MimeType != "inode/directory" && i.Name.Contains("level1"));
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_FolderHasCorrectStatistics()
    {
        var rootScope = $"stats-test-{Guid.NewGuid():N}";
        var subScope = $"{rootScope}/sub";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        var file1 = await UploadMediaAsync(imageBytes, "stats1.png", subScope);
        var file2 = await UploadMediaAsync(imageBytes, "stats2.png", subScope);

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        var folder = items!.SingleOrDefault(i => i.MimeType == "inode/directory" && i.ScopeUid == subScope);
        folder.Should().NotBeNull();

        // Folder size should be sum of files
        folder!.Size.Should().Be(file1.Size + file2.Size);

        // Folder should have a Name (human-readable)
        folder.Name.Should().NotBeNullOrEmpty();

        // Folder should have CreatedAt set
        folder.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_FolderItemCountIsCorrect()
    {
        // Reproduces bug: folder showed 3 items instead of 2
        // when folder contains 1 file and 1 subfolder with 1 file
        var rootScope = $"folder-count-{Guid.NewGuid():N}";
        var subScope = $"{rootScope}/subfolder";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        // Upload file directly in root folder
        await UploadMediaAsync(imageBytes, "cover.png", rootScope);

        // Upload file in subfolder
        await UploadMediaAsync(imageBytes, "banner.png", subScope);

        // Query at parent level (where rootScope is a child)
        var parentScope = rootScope.Contains('/') ? rootScope.Substring(0, rootScope.LastIndexOf('/')) : string.Empty;

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true{(string.IsNullOrEmpty(parentScope) ? string.Empty : $"&scopeUid={Uri.EscapeDataString(parentScope)}")}",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        var rootFolder = items!.SingleOrDefault(i => i.MimeType == "inode/directory" && i.ScopeUid == rootScope);
        rootFolder.Should().NotBeNull();

        // Folder Id should show total file count in entire tree: 1 file in root + 1 file in subfolder = 2
        // NOT counting the subfolder itself (which would make it 3)
        rootFolder!.Id.Should().Be(2, "folder should show total files in tree: 1 direct + 1 in subfolder = 2");
    }

    [Fact]
    public async Task GetList_WhenQueryProvided_ReturnsFlatSearchResults()
    {
        var rootScope = $"media-search-{Guid.NewGuid():N}";
        var subScope = $"{rootScope}/sub";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "needle-one.png", rootScope);
        await UploadMediaAsync(imageBytes, "needle-two.png", subScope);
        await UploadMediaAsync(imageBytes, "haystack.png", rootScope);

        var items = await GetTest<List<MediaDetailsDto>>(
            "/api/media?includeFolders=true&query=needle",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Should().OnlyContain(i => i.MimeType != "inode/directory");
        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.Name.Contains("needle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_WithOrderParam_SortsByNameAscending()
    {
        var rootScope = $"order-test-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "zebra.png", rootScope);
        await UploadMediaAsync(imageBytes, "alpha.png", rootScope);
        await UploadMediaAsync(imageBytes, "mike.png", rootScope);

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}&order=Name ASC",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Count.Should().BeGreaterThanOrEqualTo(3);

        var fileItems = items.Where(i => i.MimeType != "inode/directory").ToList();
        fileItems.Should().BeInAscendingOrder(f => f.Name);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_WithOrderParam_SortsByNameDescending()
    {
        var rootScope = $"order-desc-test-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "zebra.png", rootScope);
        await UploadMediaAsync(imageBytes, "alpha.png", rootScope);
        await UploadMediaAsync(imageBytes, "mike.png", rootScope);

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}&order=Name DESC",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Count.Should().BeGreaterThanOrEqualTo(3);

        var fileItems = items.Where(i => i.MimeType != "inode/directory").ToList();
        fileItems.Should().BeInDescendingOrder(f => f.Name);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_WithOrderBySize_SortsBySize()
    {
        var rootScope = $"order-size-test-{Guid.NewGuid():N}";
        var smallImage = LoadEmbeddedResource("cover-sample.png");

        // Create files - same image but different names
        await UploadMediaAsync(smallImage, "file-a.png", rootScope);
        await UploadMediaAsync(smallImage, "file-b.png", rootScope);

        var itemsAsc = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}&order=Size ASC",
            HttpStatusCode.OK);

        itemsAsc.Should().NotBeNull();
        var fileItemsAsc = itemsAsc!.Where(i => i.MimeType != "inode/directory").ToList();
        fileItemsAsc.Should().BeInAscendingOrder(f => f.Size);

        var itemsDesc = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}&order=Size DESC",
            HttpStatusCode.OK);

        itemsDesc.Should().NotBeNull();
        var fileItemsDesc = itemsDesc!.Where(i => i.MimeType != "inode/directory").ToList();
        fileItemsDesc.Should().BeInDescendingOrder(f => f.Size);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_WithNoOrderParam_DefaultsToNameAsc()
    {
        var rootScope = $"default-order-test-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "zebra.png", rootScope);
        await UploadMediaAsync(imageBytes, "alpha.png", rootScope);
        await UploadMediaAsync(imageBytes, "mike.png", rootScope);

        // No order param - should default to Name ASC
        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}",
            HttpStatusCode.OK);

        items.Should().NotBeNull();

        var fileItems = items!.Where(i => i.MimeType != "inode/directory").ToList();
        fileItems.Should().BeInAscendingOrder(f => f.Name);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_EmptyFolder_ReturnsEmptyList()
    {
        var emptyScope = $"empty-folder-{Guid.NewGuid():N}";

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(emptyScope)}",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersTrue_FoldersAndFilesMixedSorting()
    {
        var rootScope = $"mixed-sort-test-{Guid.NewGuid():N}";
        var subFolder1 = $"{rootScope}/aaa-folder";
        var subFolder2 = $"{rootScope}/zzz-folder";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        // Create files in root and subfolders
        await UploadMediaAsync(imageBytes, "mmm-file.png", rootScope);
        await UploadMediaAsync(imageBytes, "sub1.png", subFolder1);
        await UploadMediaAsync(imageBytes, "sub2.png", subFolder2);

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=true&scopeUid={Uri.EscapeDataString(rootScope)}&order=Name ASC",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Count.Should().Be(3); // 2 folders + 1 file

        // Folders get humanized names ("Aaa Folder", "Zzz Folder") and file keeps original name
        // Sorting is case-insensitive: Aaa < mmm < Zzz
        items.Should().BeInAscendingOrder(i => i.Name, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersFalse_WithScopeUidFilter_ReturnsOnlyMatchingFiles()
    {
        var scopeA = $"scope-filter-a-{Guid.NewGuid():N}";
        var scopeB = $"scope-filter-b-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "file-a1.png", scopeA);
        await UploadMediaAsync(imageBytes, "file-a2.png", scopeA);
        await UploadMediaAsync(imageBytes, "file-b1.png", scopeB);

        var items = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?includeFolders=false&filter[where][ScopeUid][eq]={Uri.EscapeDataString(scopeA)}",
            HttpStatusCode.OK);

        items.Should().NotBeNull();
        items!.Count.Should().Be(2);
        items.Should().OnlyContain(i => i.ScopeUid == scopeA);
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersFalse_WithLimitAndSkip_ReturnsPaginatedResults()
    {
        var scopeUid = $"pagination-test-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "page1.png", scopeUid);
        await UploadMediaAsync(imageBytes, "page2.png", scopeUid);
        await UploadMediaAsync(imageBytes, "page3.png", scopeUid);
        await UploadMediaAsync(imageBytes, "page4.png", scopeUid);

        // Get first page (2 items)
        var page1Response = await GetTest(
            $"/api/media?filter[where][ScopeUid][eq]={Uri.EscapeDataString(scopeUid)}&filter[order]=Name ASC&filter[limit]=2&filter[skip]=0",
            HttpStatusCode.OK);

        page1Response.Headers.TryGetValues(ResponseHeaderNames.TotalCount, out var totalCountValues)
            .Should().BeTrue();
        totalCountValues!.Single().Should().Be("4");

        var page1 = await page1Response.Content.ReadFromJsonAsync<List<MediaDetailsDto>>();
        page1.Should().NotBeNull();
        page1!.Count.Should().Be(2);

        // Get second page (2 items)
        var page2 = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][ScopeUid][eq]={Uri.EscapeDataString(scopeUid)}&filter[order]=Name ASC&filter[limit]=2&filter[skip]=2",
            HttpStatusCode.OK);

        page2.Should().NotBeNull();
        page2!.Count.Should().Be(2);

        // Pages should not overlap
        page1.Select(p => p.Id).Should().NotIntersectWith(page2.Select(p => p.Id));
    }

    [Fact]
    public async Task GetList_WhenIncludeFoldersFalse_WithOrderFilter_SortsResults()
    {
        var scopeUid = $"order-flat-test-{Guid.NewGuid():N}";
        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "charlie.png", scopeUid);
        await UploadMediaAsync(imageBytes, "alpha.png", scopeUid);
        await UploadMediaAsync(imageBytes, "bravo.png", scopeUid);

        var itemsAsc = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][ScopeUid][eq]={Uri.EscapeDataString(scopeUid)}&filter[order]=Name ASC",
            HttpStatusCode.OK);

        itemsAsc.Should().NotBeNull();
        itemsAsc!.Should().BeInAscendingOrder(i => i.Name);

        var itemsDesc = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][ScopeUid][eq]={Uri.EscapeDataString(scopeUid)}&filter[order]=Name DESC",
            HttpStatusCode.OK);

        itemsDesc.Should().NotBeNull();
        itemsDesc!.Should().BeInDescendingOrder(i => i.Name);
    }

    [Fact]
    public async Task OptimizeAll_ShouldUpdateImagesToPreferredFormat()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string originalFileName = "reoptimize-cover.png";
        const string scopeUid = "media-reoptimize";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        created.Extension.Should().Be(".png");
        created.MimeType.Should().Be("image/png");

        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");

        var reoptimizeResponse = await Request(HttpMethod.Post, "/api/media/optimize-all", new { });
        reoptimizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await reoptimizeResponse.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().BeGreaterThan(0);

        var mediaList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={scopeUid}",
            HttpStatusCode.OK);

        mediaList.Should().NotBeNull();
        var updated = mediaList!.Single(m => m.OriginalName == originalFileName);
        updated.Extension.Should().Be(".avif");
        updated.MimeType.Should().Be("image/avif");
        updated.Name.Should().EndWith(".avif");
    }

    [Fact]
    public async Task OptimizeAll_WhenDimensionsMissing_PopulatesDimensions()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string originalFileName = "reoptimize-missing-dimensions.png";
        const string scopeUid = "media-reoptimize-missing-dimensions";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        var dbContext = App.GetDbContext();
        var mediaEntity = dbContext!.Media!.Single(m => m.ScopeUid == scopeUid && m.Name == originalFileName);
        mediaEntity.Width = null;
        mediaEntity.Height = null;
        mediaEntity.OriginalWidth = null;
        mediaEntity.OriginalHeight = null;
        await dbContext.SaveChangesAsync();

        var beforeList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={scopeUid}",
            HttpStatusCode.OK);

        beforeList.Should().NotBeNull();
        var before = beforeList!.Single(m => m.Name == originalFileName);
        before.Width.Should().BeNull();
        before.Height.Should().BeNull();
        before.OriginalWidth.Should().BeNull();
        before.OriginalHeight.Should().BeNull();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");

        var reoptimizeResponse = await Request(HttpMethod.Post, "/api/media/optimize-all", new { });
        reoptimizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={scopeUid}",
            HttpStatusCode.OK);

        afterList.Should().NotBeNull();
        var after = afterList!.Single(m => m.OriginalName == originalFileName);
        after.Extension.Should().Be(".avif");
        after.MimeType.Should().Be("image/avif");
        after.Width.Should().NotBeNull();
        after.Height.Should().NotBeNull();
        after.OriginalWidth.Should().NotBeNull();
        after.OriginalHeight.Should().NotBeNull();
        after.OriginalWidth.Should().Be(736);
        after.OriginalHeight.Should().Be(404);
    }

    [Fact]
    public async Task Patch_WhenOptimisationEnabled_ShouldRenameAndUpdateContentReferences()
    {
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-patch-rename";
        const string originalFileName = "patch-rename.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        var content = new ContentCreateDto
        {
            Title = "Patch Rename Content",
            Description = "Description long enough for patch rename test",
            Body = $"Body reference /api/media/{scopeUid}/{media.Name}",
            Slug = "patch-rename-content",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{media.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "120x80");

        var patched = await PatchMediaAsync(imageBytes, "patch-rename.webp", scopeUid, media.Name);
        patched.Name.Should().EndWith(".webp");

        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{patched.Name}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{patched.Name}");
    }

    [Fact]
    public async Task RenameMedia_ShouldUpdateContentReferencesAndOriginalName()
    {
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-rename";
        const string originalFileName = "rename-cover.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        media.OriginalName.Should().Be(originalFileName);

        var content = new ContentCreateDto
        {
            Title = "Rename Media Content",
            Description = "Description long enough for rename test",
            Body = $"Cover link /api/media/{scopeUid}/{media.Name} and /media/{scopeUid}/{media.Name}.",
            Slug = "rename-media-content",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{media.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        var renameRequest = new MediaRenameRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
            NewScopeUid = "media-rename-new",
            NewFileName = "rename-cover.webp",
        };

        var renameResponse = await PostTest<MediaDetailsDto>("/api/media/rename", renameRequest, HttpStatusCode.OK);
        renameResponse.Should().NotBeNull();
        renameResponse!.UsageCount.Should().Be(3);
        renameResponse.ScopeUid.Should().Be(renameRequest.NewScopeUid);
        renameResponse.Name.Should().Be(renameRequest.NewFileName);
        renameResponse.OriginalName.Should().Be("rename-cover.png");

        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{renameRequest.NewScopeUid}/{renameRequest.NewFileName}");
        updatedContent.Body.Should().Contain($"/api/media/{renameRequest.NewScopeUid}/{renameRequest.NewFileName}");
        updatedContent.Body.Should().Contain($"/media/{renameRequest.NewScopeUid}/{renameRequest.NewFileName}");
    }

    [Fact]
    public async Task RenameMedia_ShouldReplaceBothOriginalAndCurrentLinksWithNewCurrentLink()
    {
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-rename-both";
        const string originalFileName = "both-links.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Verify we have both original and current names
        media.OriginalName.Should().Be(originalFileName);
        media.Name.Should().EndWith(".webp");
        media.Name.Should().NotBe(originalFileName);

        // Create content with references to BOTH the original name and the current (optimized) name
        var content = new ContentCreateDto
        {
            Title = "Both Links Content",
            Description = "Description long enough for both links test",
            Body = $"Original link: /api/media/{scopeUid}/{media.OriginalName} and current link: /api/media/{scopeUid}/{media.Name}.",
            Slug = "both-links-content",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{media.OriginalName}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Verify the content has both types of links
        createdContent!.Body.Should().Contain($"/api/media/{scopeUid}/{media.OriginalName}");
        createdContent.Body.Should().Contain($"/api/media/{scopeUid}/{media.Name}");
        createdContent.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{media.OriginalName}");

        // Rename the media
        var renameRequest = new MediaRenameRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
            NewScopeUid = "media-rename-both-new",
            NewFileName = "renamed-both.webp",
        };

        var renameResponse = await PostTest<MediaDetailsDto>("/api/media/rename", renameRequest, HttpStatusCode.OK);
        renameResponse.Should().NotBeNull();

        // The new OriginalName should be the new file name with the original extension
        renameResponse!.OriginalName.Should().Be("renamed-both.png");
        renameResponse.Name.Should().Be(renameRequest.NewFileName);

        // Verify both old links (original and current) are replaced with the NEW CURRENT link
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();

        // CoverImageUrl (which had original name) should now have new current name
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{renameRequest.NewScopeUid}/{renameRequest.NewFileName}");

        // Body should NOT contain old original link
        updatedContent.Body.Should().NotContain($"/api/media/{scopeUid}/{media.OriginalName}");

        // Body should NOT contain old current link
        updatedContent.Body.Should().NotContain($"/api/media/{scopeUid}/{media.Name}");

        // Body SHOULD contain the new current link (both old links replaced with new current)
        updatedContent.Body.Should().Contain($"/api/media/{renameRequest.NewScopeUid}/{renameRequest.NewFileName}");

        // Verify we have exactly 2 occurrences of the new link in the body (both old links replaced)
        var newLinkPattern = $"/api/media/{renameRequest.NewScopeUid}/{renameRequest.NewFileName}";
        var occurrences = updatedContent.Body!.Split(newLinkPattern).Length - 1;
        occurrences.Should().Be(2, "both the original name link and current name link should be replaced with new current link");
    }

    [Fact]
    public async Task Upload_NewFile_WithUppercaseName_ShouldUpdateContentReferencesToLowercase()
    {
        // Case 1: Content references "UPPERCASE.png" (the raw upload name).
        // After upload, the file is stored as "uppercase.png" (slugified).
        // Content references should be updated from UPPERCASE.png to uppercase.png.
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");

        const string scopeUid = "upload-slug-case";
        const string rawFileName = "UPPERCASE-IMAGE.png";
        var expectedStoredName = rawFileName.ToTranslit().Slugify(); // "uppercase-image.png"

        // Step 1: Create content that references the raw file name
        var content = new ContentCreateDto
        {
            Title = "Content With Uppercase Media",
            Description = "Description long enough for slugify rename test content",
            Body = $"Image link: /api/media/{scopeUid}/{rawFileName} and /media/{scopeUid}/{rawFileName}.",
            Slug = "content-uppercase-media",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{rawFileName}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Step 2: Upload the file with uppercase name (simulating SDK bulk upload)
        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, rawFileName, scopeUid);

        media.Name.Should().Be(expectedStoredName);

        // Step 3: Verify content references are updated to the slugified name
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{expectedStoredName}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{expectedStoredName}");
        updatedContent.Body.Should().Contain($"/media/{scopeUid}/{expectedStoredName}");
        updatedContent.Body.Should().NotContain(rawFileName);
    }

    [Fact]
    public async Task Upload_NewFile_WithUppercaseName_OptimisedToAvif_ShouldUpdateContentReferences()
    {
        // Case 2: Content references "UPPERCASE.png" (the raw upload name).
        // After upload + optimization, file is stored as "uppercase.avif".
        // Content references should update from UPPERCASE.png to uppercase.avif.
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "upload-slug-optim";
        const string rawFileName = "UPPERCASE-PHOTO.png";

        // Step 1: Create content that references the raw file name
        var content = new ContentCreateDto
        {
            Title = "Content With Optimized Media",
            Description = "Description long enough for optimized slugify rename test",
            Body = $"Image link: /api/media/{scopeUid}/{rawFileName} and /media/{scopeUid}/{rawFileName}.",
            Slug = "content-optimized-media",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{rawFileName}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Step 2: Upload the file with uppercase name
        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, rawFileName, scopeUid);

        // The stored name should be slugified and have the optimized extension
        media.Name.Should().EndWith(".avif");
        media.OriginalName.Should().NotBeNull();

        // Step 3: Verify content references are updated to the final stored name
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{media.Name}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{media.Name}");
        updatedContent.Body.Should().Contain($"/media/{scopeUid}/{media.Name}");
        updatedContent.Body.Should().NotContain(rawFileName);
    }

    [Fact]
    public async Task Upload_ExistingFile_WithExtensionChange_ShouldUpdateContentReferences()
    {
        // Case 3: A file already exists as "image.png" (optimization was off).
        // Now re-uploaded with optimization enabled, becoming "image.avif".
        // Content references should update from image.png to image.avif.
        TrackEntityType<Content>();

        // First upload without optimization
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");

        const string scopeUid = "upload-ext-change";
        const string originalFileName = "test-image.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);
        media.Name.Should().Be(originalFileName);

        // Create content that references the existing file
        var content = new ContentCreateDto
        {
            Title = "Content With Existing Media",
            Description = "Description long enough for extension change rename test",
            Body = $"Image link: /api/media/{scopeUid}/{originalFileName} and /media/{scopeUid}/{originalFileName}.",
            Slug = "content-existing-media",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{originalFileName}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Now enable optimization and re-upload the same file
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        var updatedMedia = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);
        updatedMedia.Name.Should().EndWith(".avif");

        // Verify content references are updated to the new extension
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{updatedMedia.Name}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{updatedMedia.Name}");
        updatedContent.Body.Should().Contain($"/media/{scopeUid}/{updatedMedia.Name}");
        updatedContent.Body.Should().NotContain(originalFileName);
    }

    [Fact]
    public async Task RenameFolder_ShouldUpdateContentReferences_AndCreateSingleChangeLogEntry()
    {
        TrackEntityType<Content>();

        const string folder = "media-rename-folder";
        const string childFolder = "media-rename-folder/child";
        const string newFolder = "media-renamed-folder";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        var mediaParent = await UploadMediaAsync(imageBytes, "parent.png", folder);
        var mediaChild = await UploadMediaAsync(imageBytes, "child.png", childFolder);

        var content = new ContentCreateDto
        {
            Title = "Rename Folder Content",
            Description = "Description long enough for rename folder test",
            Body = $"Links /api/media/{folder}/{mediaParent.Name} and /api/media/{childFolder}/{mediaChild.Name}.",
            Slug = "rename-folder-content",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{folder}/{mediaParent.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        var renameRequest = new MediaBulkRenameRequestDto
        {
            Folder = folder,
            NewFolder = newFolder,
        };

        var response = await Request(HttpMethod.Post, "/api/media/rename-folder", renameRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().Be(2);

        var updatedParentList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(newFolder)}",
            HttpStatusCode.OK);
        updatedParentList.Should().NotBeNull();
        updatedParentList!.Should().ContainSingle(m => m.Name == mediaParent.Name);

        var updatedChildList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(newFolder + "/child")}",
            HttpStatusCode.OK);
        updatedChildList.Should().NotBeNull();
        updatedChildList!.Should().ContainSingle(m => m.Name == mediaChild.Name);

        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{newFolder}/{mediaParent.Name}");
        updatedContent.Body.Should().Contain($"/api/media/{newFolder}/{mediaParent.Name}");
        updatedContent.Body.Should().Contain($"/api/media/{newFolder}/child/{mediaChild.Name}");
        updatedContent.Body.Should().NotContain($"/api/media/{folder}/{mediaParent.Name}");
        updatedContent.Body.Should().NotContain($"/api/media/{childFolder}/{mediaChild.Name}");

        var dbContext = App.GetDbContext();
        var changeLogs = dbContext!.ChangeLogs!
            .Where(c => c.ObjectType == "Content" && c.ObjectId == createdContent.Id && c.EntityState == EntityState.Modified)
            .ToList();

        changeLogs.Count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteFolder_ShouldDeleteRecursively()
    {
        const string folder = "media-delete-folder";
        const string childFolder = "media-delete-folder/child";
        const string otherFolder = "media-delete-other";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        await UploadMediaAsync(imageBytes, "parent.png", folder);
        await UploadMediaAsync(imageBytes, "child.png", childFolder);
        await UploadMediaAsync(imageBytes, "other.png", otherFolder);

        var request = new MediaBulkDeleteRequestDto
        {
            Folder = folder,
        };

        var response = await Request(HttpMethod.Post, "/api/media/delete-folder", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().Be(2);

        var parentAfter = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(folder)}",
            HttpStatusCode.OK);
        parentAfter.Should().NotBeNull();
        parentAfter!.Should().BeEmpty();

        var childAfter = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(childFolder)}",
            HttpStatusCode.OK);
        childAfter.Should().NotBeNull();
        childAfter!.Should().BeEmpty();

        var otherAfter = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(otherFolder)}",
            HttpStatusCode.OK);
        otherAfter.Should().NotBeNull();
        otherAfter!.Should().ContainSingle();
    }

    [Fact]
    public async Task OptimizeMedia_ShouldUpdateDimensionsAndRespectLogin()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "120x80");

        const string scopeUid = "media-optimize";
        const string originalFileName = "optimize-cover.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");

        var request = new MediaTransformRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
        };

        Logout();
        var unauthorized = await Request(HttpMethod.Post, "/api/media/optimize", request);
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await LoginAsAdmin();
        var response = await Request(HttpMethod.Post, "/api/media/optimize", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var optimized = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        optimized.Should().NotBeNull();
        optimized!.Extension.Should().Be(".webp");
        optimized.Width.Should().BeGreaterThan(0);
        optimized.Height.Should().BeGreaterThan(0);
        optimized.Width.Should().BeLessThanOrEqualTo(120);
        optimized.Height.Should().BeLessThanOrEqualTo(80);
    }

    [Fact]
    public async Task OptimizeMedia_ShouldWorkEvenWhenOptimisationDisabled()
    {
        // Manual optimization via /optimize endpoint should work regardless of MediaEnableOptimisation setting
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "100x100");

        const string scopeUid = "media-optimize-when-disabled";
        const string originalFileName = "optimize-when-disabled.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Keep optimization disabled - the /optimize endpoint should still work
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");

        var request = new MediaTransformRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
        };

        var response = await Request(HttpMethod.Post, "/api/media/optimize", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var optimized = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        optimized.Should().NotBeNull();
        optimized!.Extension.Should().Be(".webp");
        optimized.Width.Should().BeGreaterThan(0);
        optimized.Height.Should().BeGreaterThan(0);
        optimized.Width.Should().BeLessThanOrEqualTo(100);
        optimized.Height.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task OptimizeAll_ShouldWorkEvenWhenOptimisationDisabled()
    {
        // Manual optimization via /optimize-all endpoint should work regardless of MediaEnableOptimisation setting
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string originalFileName = "reoptimize-when-disabled.png";
        const string scopeUid = "media-reoptimize-when-disabled";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Keep optimization disabled but change preferred format
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");

        var reoptimizeResponse = await Request(HttpMethod.Post, "/api/media/optimize-all", new { });
        reoptimizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await reoptimizeResponse.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().BeGreaterThan(0);

        // Verify media was actually optimized
        var afterList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={scopeUid}",
            HttpStatusCode.OK);

        afterList.Should().NotBeNull();
        var after = afterList!.Single(m => m.OriginalName == originalFileName);
        after.Extension.Should().Be(".avif");
        after.MimeType.Should().Be("image/avif");
    }

    [Fact]
    public async Task OptimizeAll_WithFolderFilter_ShouldOnlyOptimizeFilesInFolder()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        // Upload to different folders
        var folder1 = "optimize-folder/subfolder1";
        var folder2 = "optimize-folder/subfolder2";
        var rootFolder = "optimize-folder";

        await UploadMediaAsync(imageBytes, "image1.png", folder1);
        await UploadMediaAsync(imageBytes, "image2.png", folder2);
        await UploadMediaAsync(imageBytes, "image3.png", rootFolder);

        // Change format to avif
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");

        // Optimize only subfolder1
        var request = new MediaBulkOptimizeRequestDto { Folder = folder1, IncludeSubfolders = false };
        var response = await Request(HttpMethod.Post, "/api/media/optimize-all", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().Be(1);

        // Verify only folder1 was optimized
        var list1 = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(folder1)}", HttpStatusCode.OK);
        list1!.Should().ContainSingle();
        list1!.First().Extension.Should().Be(".avif");

        var list2 = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(folder2)}", HttpStatusCode.OK);
        list2!.Should().ContainSingle();
        list2!.First().Extension.Should().Be(".png");
    }

    [Fact]
    public async Task OptimizeAll_WithFolderAndIncludeSubfolders_ShouldOptimizeRecursively()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        var parentFolder = "optimize-recursive";
        var childFolder = "optimize-recursive/child";
        var unrelatedFolder = "optimize-other";

        await UploadMediaAsync(imageBytes, "parent.png", parentFolder);
        await UploadMediaAsync(imageBytes, "child.png", childFolder);
        await UploadMediaAsync(imageBytes, "other.png", unrelatedFolder);

        // Change format to webp
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");

        // Optimize parent folder with subfolders
        var request = new MediaBulkOptimizeRequestDto { Folder = parentFolder, IncludeSubfolders = true };
        var response = await Request(HttpMethod.Post, "/api/media/optimize-all", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().Be(2);

        // Verify parent and child were optimized
        var parentList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(parentFolder)}", HttpStatusCode.OK);
        parentList!.First().Extension.Should().Be(".webp");

        var childList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(childFolder)}", HttpStatusCode.OK);
        childList!.First().Extension.Should().Be(".webp");

        // Verify unrelated folder was NOT optimized
        var otherList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(unrelatedFolder)}", HttpStatusCode.OK);
        otherList!.First().Extension.Should().Be(".png");
    }

    [Fact]
    public async Task ResetMedia_ShouldRevertToOriginalState()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-reset-single";
        const string originalFileName = "reset-test.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Verify it was optimized
        created.Extension.Should().Be(".avif");
        created.OriginalExtension.Should().Be(".png");

        // Reset the media
        var request = new MediaTransformRequestDto
        {
            ScopeUid = scopeUid,
            FileName = created.Name,
        };

        var response = await Request(HttpMethod.Post, "/api/media/reset", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reset = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        reset.Should().NotBeNull();
        reset!.Extension.Should().Be(".png");
        reset.MimeType.Should().Be("image/png");
        reset.OriginalExtension.Should().BeNull();
    }

    [Fact]
    public async Task ResetMedia_WithoutOriginalData_ShouldReturnUnprocessableEntity()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");

        const string scopeUid = "media-reset-no-original";
        const string originalFileName = "no-original.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var created = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Verify no original data
        created.OriginalExtension.Should().BeNull();

        // Try to reset - should fail
        var request = new MediaTransformRequestDto
        {
            ScopeUid = scopeUid,
            FileName = created.Name,
        };

        var response = await Request(HttpMethod.Post, "/api/media/reset", request);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ResetAll_ShouldRevertAllOptimizedMedia()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-reset-all";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        await UploadMediaAsync(imageBytes, "reset1.png", scopeUid);
        await UploadMediaAsync(imageBytes, "reset2.png", scopeUid);

        // Verify they were optimized
        var beforeList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={scopeUid}", HttpStatusCode.OK);
        beforeList!.Should().HaveCount(2);
        beforeList!.Should().OnlyContain(m => m.Extension == ".avif");

        // Reset all
        var response = await Request(HttpMethod.Post, "/api/media/reset-all", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().BeGreaterThanOrEqualTo(2);

        // Verify all were reset
        var afterList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={scopeUid}", HttpStatusCode.OK);
        afterList!.Should().HaveCount(2);
        afterList!.Should().OnlyContain(m => m.Extension == ".png" && m.OriginalExtension == null);
    }

    [Fact]
    public async Task ResetAll_WithFolderFilter_ShouldOnlyResetFilesInFolder()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        var folder1 = "reset-folder1";
        var folder2 = "reset-folder2";

        await UploadMediaAsync(imageBytes, "f1.png", folder1);
        await UploadMediaAsync(imageBytes, "f2.png", folder2);

        // Verify both optimized
        var f1Before = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={folder1}", HttpStatusCode.OK);
        f1Before!.First().Extension.Should().Be(".webp");

        // Reset only folder1
        var request = new MediaBulkResetRequestDto { Folder = folder1, IncludeSubfolders = false };
        var response = await Request(HttpMethod.Post, "/api/media/reset-all", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().Be(1);

        // Verify only folder1 was reset
        var f1After = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={folder1}", HttpStatusCode.OK);
        f1After!.First().Extension.Should().Be(".png");

        var f2After = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={folder2}", HttpStatusCode.OK);
        f2After!.First().Extension.Should().Be(".webp");
    }

    [Fact]
    public async Task ResetAll_WithFolderAndIncludeSubfolders_ShouldResetRecursively()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        var imageBytes = LoadEmbeddedResource("cover-sample.png");

        var parentFolder = "reset-recursive";
        var childFolder = "reset-recursive/child";
        var unrelatedFolder = "reset-other";

        await UploadMediaAsync(imageBytes, "parent.png", parentFolder);
        await UploadMediaAsync(imageBytes, "child.png", childFolder);
        await UploadMediaAsync(imageBytes, "other.png", unrelatedFolder);

        // Verify all optimized
        var parentBefore = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(parentFolder)}", HttpStatusCode.OK);
        parentBefore!.First().Extension.Should().Be(".avif");

        // Reset parent folder with subfolders
        var request = new MediaBulkResetRequestDto { Folder = parentFolder, IncludeSubfolders = true };
        var response = await Request(HttpMethod.Post, "/api/media/reset-all", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MediaOptimizeResponseDto>();
        result.Should().NotBeNull();
        result!.Updated.Should().Be(2);

        // Verify parent and child were reset
        var parentAfter = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(parentFolder)}", HttpStatusCode.OK);
        parentAfter!.First().Extension.Should().Be(".png");

        var childAfter = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(childFolder)}", HttpStatusCode.OK);
        childAfter!.First().Extension.Should().Be(".png");

        // Verify unrelated folder was NOT reset
        var otherAfter = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={Uri.EscapeDataString(unrelatedFolder)}", HttpStatusCode.OK);
        otherAfter!.First().Extension.Should().Be(".avif");
    }

    [Fact]
    public async Task OptimizeMedia_ShouldUpdateContentReferences_WhenNameChanges()
    {
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-optimize-content-ref";
        const string originalFileName = "optimize-ref.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Create content referencing the media
        var content = new ContentCreateDto
        {
            Title = "Optimize Content Reference",
            Description = "Description for optimize content reference test",
            Body = $"Image reference /api/media/{scopeUid}/{media.Name}",
            Slug = "optimize-content-ref",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{media.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Now optimize the media - it should change to avif
        var request = new MediaTransformRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
        };

        var response = await Request(HttpMethod.Post, "/api/media/optimize", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var optimized = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        optimized.Should().NotBeNull();
        optimized!.Extension.Should().Be(".avif");

        // Verify content references were updated
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{optimized.Name}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{optimized.Name}");
        updatedContent.Body.Should().NotContain($"/api/media/{scopeUid}/{media.Name}");
    }

    [Fact]
    public async Task ResetMedia_ShouldUpdateContentReferences_WhenNameChanges()
    {
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-reset-content-ref";
        const string originalFileName = "reset-ref.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Verify it was optimized
        media.Extension.Should().Be(".webp");
        media.OriginalName.Should().Be(originalFileName);

        // Create content referencing the optimized media
        var content = new ContentCreateDto
        {
            Title = "Reset Content Reference",
            Description = "Description for reset content reference test",
            Body = $"Image reference /api/media/{scopeUid}/{media.Name}",
            Slug = "reset-content-ref",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{media.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Now reset the media - it should revert to original name
        var request = new MediaTransformRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
        };

        var response = await Request(HttpMethod.Post, "/api/media/reset", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var reset = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        reset.Should().NotBeNull();
        reset!.Extension.Should().Be(".png");
        reset.Name.Should().Be(originalFileName);

        // Verify content references were updated
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{originalFileName}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{originalFileName}");
        updatedContent.Body.Should().NotContain($"/api/media/{scopeUid}/{media.Name}");
    }

    [Fact]
    public async Task OptimizeAll_ShouldUpdateContentReferences_WhenNamesChange()
    {
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-optimize-all-ref";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, "optimize-all-ref.png", scopeUid);

        // Create content referencing the media
        var content = new ContentCreateDto
        {
            Title = "Optimize All Content Reference",
            Description = "Description for optimize all content reference test",
            Body = $"Image reference /api/media/{scopeUid}/{media.Name}",
            Slug = "optimize-all-content-ref",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{media.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Change format and optimize all
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");

        var optimizeRequest = new MediaBulkOptimizeRequestDto { Folder = scopeUid };
        var response = await Request(HttpMethod.Post, "/api/media/optimize-all", optimizeRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get the updated media
        var mediaList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={scopeUid}", HttpStatusCode.OK);
        var optimized = mediaList!.First();
        optimized.Extension.Should().Be(".avif");

        // Verify content references were updated
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{optimized.Name}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{optimized.Name}");
    }

    [Fact]
    public async Task ResetAll_ShouldUpdateContentReferences_WhenNamesChange()
    {
        TrackEntityType<Content>();

        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-reset-all-ref";
        const string originalFileName = "reset-all-ref.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        // Verify it was optimized
        media.Extension.Should().Be(".webp");
        media.OriginalName.Should().Be(originalFileName);

        // Create content referencing the optimized media
        var content = new ContentCreateDto
        {
            Title = "Reset All Content Reference",
            Description = "Description for reset all content reference test",
            Body = $"Image reference /api/media/{scopeUid}/{media.Name}",
            Slug = "reset-all-content-ref",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{scopeUid}/{media.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content, HttpStatusCode.Created);
        createdContent.Should().NotBeNull();

        // Reset all in this folder
        var resetRequest = new MediaBulkResetRequestDto { Folder = scopeUid };
        var response = await Request(HttpMethod.Post, "/api/media/reset-all", resetRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Get the updated media
        var mediaList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={scopeUid}", HttpStatusCode.OK);
        var reset = mediaList!.First();
        reset.Extension.Should().Be(".png");
        reset.Name.Should().Be(originalFileName);

        // Verify content references were updated
        var updatedContent = await GetTest<ContentDetailsDto>($"/api/content/{createdContent!.Id}", HttpStatusCode.OK);
        updatedContent.Should().NotBeNull();
        updatedContent!.CoverImageUrl.Should().Be($"/api/media/{scopeUid}/{originalFileName}");
        updatedContent.Body.Should().Contain($"/api/media/{scopeUid}/{originalFileName}");
        updatedContent.Body.Should().NotContain($"/api/media/{scopeUid}/{media.Name}");
    }

    [Fact]
    public async Task OptimizeMedia_ShouldApplyCoverDimensions_WhenCoverTagPresent()
    {
        // Cover dimensions are 1200x630 by default
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");
        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "400x200");

        const string scopeUid = "media-optimize-cover";
        const string originalFileName = "optimize-with-cover-tag.png";

        // Upload with cover tag
        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaWithTagsAsync(imageBytes, originalFileName, scopeUid, new[] { "cover" });

        media.Should().NotBeNull();
        media!.Tags.Should().Contain("cover");

        // The image should be cropped to cover dimensions (400x200) since it has the cover tag
        media.Width.Should().Be(400);
        media.Height.Should().Be(200);

        // Now change settings to different cover dimensions
        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "300x150");

        // Re-optimize the media - it should apply the new cover dimensions
        var request = new MediaTransformRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
        };

        var response = await Request(HttpMethod.Post, "/api/media/optimize", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var optimized = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        optimized.Should().NotBeNull();
        optimized!.Width.Should().Be(300);
        optimized.Height.Should().Be(150);
    }

    [Fact]
    public async Task UploadMedia_ShouldApplyCoverDimensions_WhenCoverResizeEnabled()
    {
        // Explicitly enable cover resize and set dimensions
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaEnableCoverResize, "true");
        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "300x150");

        const string scopeUid = "media-cover-resize-enabled";
        const string originalFileName = "cover-resize-enabled.png";

        // Upload with cover tag
        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaWithTagsAsync(imageBytes, originalFileName, scopeUid, new[] { "cover" });

        media.Should().NotBeNull();
        media!.Tags.Should().Contain("cover");

        // The image should be cropped to cover dimensions since cover resize is enabled
        media.Width.Should().Be(300);
        media.Height.Should().Be(150);
    }

    [Fact]
    public async Task UploadMedia_ShouldNotApplyCoverDimensions_WhenCoverResizeDisabled()
    {
        // Disable cover resize
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaEnableCoverResize, "false");
        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "300x150");

        const string scopeUid = "media-cover-resize-disabled";
        const string originalFileName = "cover-resize-disabled.png";

        // Upload with cover tag
        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaWithTagsAsync(imageBytes, originalFileName, scopeUid, new[] { "cover" });

        media.Should().NotBeNull();
        media!.Tags.Should().Contain("cover");

        // The image should NOT be cropped to cover dimensions since cover resize is disabled
        // It should retain its original dimensions (cover-sample.png is 736x404)
        media.Width.Should().Be(736);
        media.Height.Should().Be(404);
    }

    [Fact]
    public async Task ResizeMedia_ShouldPreserveOriginal_WhenMissing()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-resize-preserve-original";
        const string originalFileName = "resize-preserve-original.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        media.OriginalName.Should().BeNull();

        var request = new MediaResizeRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
            Width = 140,
            Height = 90,
            MaintainAspectRatio = false,
        };

        var response = await Request(HttpMethod.Post, "/api/media/resize", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resized = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        resized.Should().NotBeNull();
        resized!.Width.Should().Be(140);
        resized.Height.Should().Be(90);
        resized.OriginalName.Should().Be(originalFileName);
        resized.OriginalSize.Should().NotBeNull();
        resized.OriginalExtension.Should().Be(".png");
        resized.OriginalMimeType.Should().Be("image/png");
        resized.OriginalWidth.Should().NotBeNull();
        resized.OriginalHeight.Should().NotBeNull();
        resized.OriginalWidth.Should().Be(736);
        resized.OriginalHeight.Should().Be(404);

        var mediaList = await GetTest<List<MediaDetailsDto>>(
            $"/api/media?filter[where][scopeUid][eq]={scopeUid}",
            HttpStatusCode.OK);

        mediaList.Should().NotBeNull();
        var persisted = mediaList!.Single(m => m.Name == resized.Name);
        persisted.OriginalName.Should().Be(originalFileName);
        persisted.OriginalSize.Should().NotBeNull();
        persisted.OriginalExtension.Should().Be(".png");
        persisted.OriginalMimeType.Should().Be("image/png");
        persisted.OriginalWidth.Should().NotBeNull();
        persisted.OriginalHeight.Should().NotBeNull();
        persisted.OriginalWidth.Should().Be(736);
        persisted.OriginalHeight.Should().Be(404);
    }

    [Fact]
    public async Task ResizeMedia_ShouldUpdateWidthAndHeight()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-resize";
        const string originalFileName = "resize-cover.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        var request = new MediaResizeRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
            Width = 200,
            Height = 100,
            MaintainAspectRatio = false,
        };

        var response = await Request(HttpMethod.Post, "/api/media/resize", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resized = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        resized.Should().NotBeNull();
        resized!.Width.Should().Be(200);
        resized.Height.Should().Be(100);
    }

    [Fact]
    public async Task CropMedia_ShouldUpdateWidthAndHeight()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "png");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        const string scopeUid = "media-crop";
        const string originalFileName = "crop-cover.png";

        var imageBytes = LoadEmbeddedResource("cover-sample.png");
        var media = await UploadMediaAsync(imageBytes, originalFileName, scopeUid);

        var request = new MediaCropRequestDto
        {
            ScopeUid = scopeUid,
            FileName = media.Name,
            Width = 120,
            Height = 80,
            X = 10,
            Y = 5,
        };

        var response = await Request(HttpMethod.Post, "/api/media/crop", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cropped = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        cropped.Should().NotBeNull();
        cropped!.Width.Should().Be(120);
        cropped.Height.Should().Be(80);
    }

    public async Task<bool> CreateAndGetMedia(string fileName, int fileSize)
    {
        var testMedia = new TestMedia(fileName, fileSize);
        var postResult = await PostTest("/api/media", testMedia);
        if (!postResult.Item2)
        {
            return false;
        }

        var imageStream = await GetImageTest(postResult.Item1);
        if (imageStream == null)
        {
            return false;
        }

        if (!IsImageFile(fileName))
        {
            return CompareStreams(testMedia.DataBuffer, imageStream!);
        }

        return imageStream.Length > 0;
    }

    protected override Task<HttpResponseMessage> Request(HttpMethod method, string url, object? payload)
    {
        if (payload is not TestMedia)
        {
            return base.Request(method, url, payload);
        }

        var request = new HttpRequestMessage(method, url);

        var testMedia = (TestMedia)payload!;
        var content = new MultipartFormDataContent();
        content.Add(new StreamContent(testMedia.DataBuffer), "File", testMedia.File!.Name);
        content.Add(new StringContent(testMedia.ScopeUid), "ScopeUid");

        request.Content = content;

        request.Headers.Authorization = GetAuthenticationHeaderValue();

        return client.SendAsync(request);
    }

    private static bool IsImageFile(string fileName)
    {
        var provider = ContentTypeHelper.CreateCustomizedProvider();
        if (!provider.TryGetContentType(fileName, out var mimeType))
        {
            return false;
        }

        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] LoadEmbeddedResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePath = assembly.GetManifestResourceNames().Single(name => name.EndsWith(fileName));
        using var stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{fileName}' not found.");
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private bool CompareStreams(Stream s1, Stream s2)
    {
        if (s1.Length != s2.Length)
        {
            return false;
        }

        var s1Hash = string.Concat(SHA1.HashData(((MemoryStream)s1).ToArray()).Select(b => b.ToString("x2")));
        var s2Hash = string.Concat(SHA1.HashData(((MemoryStream)s2).ToArray()).Select(b => b.ToString("x2")));

        return string.Equals(s1Hash, s2Hash, StringComparison.Ordinal);
    }

    private async Task<(string, bool)> PostTest(string url, TestMedia payload)
    {
        var response = await Request(HttpMethod.Post, url, payload);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            return (string.Empty, false);
        }

        var mediaDetails = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        if (mediaDetails != null && !string.IsNullOrEmpty(mediaDetails.Location))
        {
            return (mediaDetails.Location, true);
        }

        return (string.Empty, false);
    }

    private async Task SetSystemSettingAsync(string key, string value)
    {
        var url = $"/api/settings/system/{Uri.EscapeDataString(key)}?value={Uri.EscapeDataString(value)}";
        var response = await Request(HttpMethod.Put, url, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<MediaDetailsDto> UploadMediaAsync(byte[] bytes, string fileName, string scopeUid)
    {
        return await UploadMediaWithTagsAsync(bytes, fileName, scopeUid, null);
    }

    private async Task<MediaDetailsDto> UploadMediaWithTagsAsync(byte[] bytes, string fileName, string scopeUid, string[]? tags)
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.TryGetContentType(fileName, out var contentType);

        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream(bytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        form.Add(fileContent, "File", fileName);
        form.Add(new StringContent(scopeUid), "ScopeUid");

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                form.Add(new StringContent(tag), "Tags");
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/media")
        {
            Content = form,
        };
        request.Headers.Authorization = GetAuthenticationHeaderValue();

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var media = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        media.Should().NotBeNull();
        media!.Location.Should().NotBeNullOrWhiteSpace();

        return media;
    }

    private async Task<MediaDetailsDto> PatchMediaAsync(byte[] bytes, string fileName, string scopeUid, string existingFileName)
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.TryGetContentType(fileName, out var contentType);

        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream(bytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        form.Add(fileContent, "File", fileName);
        form.Add(new StringContent(scopeUid), "ScopeUid");
        form.Add(new StringContent(existingFileName), "FileName");

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/media")
        {
            Content = form,
        };
        request.Headers.Authorization = GetAuthenticationHeaderValue();

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var media = await response.Content.ReadFromJsonAsync<MediaDetailsDto>();
        media.Should().NotBeNull();
        media!.Location.Should().NotBeNullOrWhiteSpace();

        return media;
    }

    private async Task<Stream?> GetImageTest(string url, HttpStatusCode expectedCode = HttpStatusCode.OK)
    {
        var response = await GetTest(url, expectedCode);

        if (expectedCode == HttpStatusCode.OK)
        {
            return await response.Content.ReadAsStreamAsync();
        }
        else
        {
            return null;
        }
    }
}