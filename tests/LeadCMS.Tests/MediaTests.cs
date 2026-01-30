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
using Microsoft.AspNetCore.StaticFiles;

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
    [InlineData("HelloWorld-ThisIs---     ...DotNet.png", "helloworld-thisis---...dotnet.png", 1024)]
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
    public async Task Reoptimize_ShouldUpdateImagesToPreferredFormat()
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

        var reoptimizeResponse = await Request(HttpMethod.Post, "/api/media/reoptimize", new { });
        reoptimizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await reoptimizeResponse.Content.ReadFromJsonAsync<MediaReoptimizeResponseDto>();
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
    public async Task Reoptimize_WhenDimensionsMissing_PopulatesDimensions()
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

        var reoptimizeResponse = await Request(HttpMethod.Post, "/api/media/reoptimize", new { });
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