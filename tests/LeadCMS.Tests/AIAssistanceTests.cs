// <copyright file="AIAssistanceTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using ImageMagick;
using LeadCMS.Constants;
using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Tests.TestServices;
using Microsoft.AspNetCore.StaticFiles;

namespace LeadCMS.Tests;

[Collection("AI Assistance Tests")]
public class AIAssistanceTests : BaseTestAutoLogin
{
    public AIAssistanceTests()
    {
        TestAIProviderService.Reset();
        TrackEntityType<Media>();
        TrackEntityType<Setting>();
    }

    [Fact]
    public async Task CoverImageGenerationEndpoint_ShouldGenerateCoverImage()
    {
        var request = new CoverImageGenerationRequest
        {
            ContentTitle = "Automated Testing in Practice",
            ContentDescription = "A practical guide to test automation strategies and tooling choices.",
            ContentSlug = "blog/automated-testing-in-practice",
            Prompt = "Generate a cover image with a laptop, code snippets, and testing icons",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover", request, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.ScopeUid.Should().Be(request.ContentSlug);
        response.Location.Should().Contain($"/api/media/{request.ContentSlug}/");

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.Prompt.Should().Contain(request.ContentTitle);
        lastRequest.Prompt.Should().Contain(request.ContentDescription);
        lastRequest.Prompt.Should().Contain(request.Prompt!);
    }

    [Fact]
    public async Task CoverImageGenerationEndpoint_ShouldOverwriteExistingCover()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");

        var request = new CoverImageGenerationRequest
        {
            ContentTitle = "Duplicate Cover Test",
            ContentDescription = "Ensure repeated generation overwrites the existing cover.",
            ContentSlug = "ai-cover-duplicate-test",
            Prompt = "Initial cover",
        };

        var first = await PostTest<MediaDetailsDto>("/api/content/ai-cover", request, HttpStatusCode.OK);
        first.Should().NotBeNull();

        request.Prompt = "Updated cover";
        var second = await PostTest<MediaDetailsDto>("/api/content/ai-cover", request, HttpStatusCode.OK);

        second.Should().NotBeNull();
        second!.Id.Should().Be(first!.Id);

        var dbContext = App.GetDbContext();
        var coverCount = dbContext!.Media!
            .Where(m => m.ScopeUid == request.ContentSlug)
            .AsEnumerable()
            .Count(m => Array.Exists(m.Tags, tag => string.Equals(tag, "cover", StringComparison.OrdinalIgnoreCase)));
        coverCount.Should().Be(1);
    }

    [Fact]
    public async Task CoverImageEditEndpoint_ShouldEditCoverImage()
    {
        var createRequest = new CoverImageGenerationRequest
        {
            ContentTitle = "Scaling Test Pipelines",
            ContentDescription = "How to scale CI pipelines while keeping feedback fast and reliable.",
            ContentSlug = "ai-cover-edit-test",
        };

        var created = await PostTest<MediaDetailsDto>("/api/content/ai-cover", createRequest, HttpStatusCode.OK);
        created.Should().NotBeNull();

        var editRequest = new CoverImageEditRequest
        {
            CoverImageUrl = created!.Location,
            ContentTitle = "Scaling Test Pipelines (Updated)",
            ContentDescription = "Refined guidance on scaling CI pipelines with minimal friction.",
            Prompt = "Add a subtle gradient",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover/edit", editRequest, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.ScopeUid.Should().Be(createRequest.ContentSlug);

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.Prompt.Should().Be(editRequest.Prompt);
        lastRequest.EditImage.Should().NotBeNull();
        lastRequest.EditImage!.FileName.Should().EndWith(".png");
        lastRequest.EditImage.MimeType.Should().Be("image/png");
    }

    [Fact]
    public async Task CoverImageEdit_UsesOriginalImageWhenAvailable()
    {
        var originalBytes = LoadEmbeddedResource("cover-sample.png");
        var createdCover = await UploadMediaAsync(originalBytes, "optimized-cover.avif", "blog/article/original-edit");

        var dbContext = App.GetDbContext();
        var media = dbContext!.Media!
            .First(m => m.ScopeUid == createdCover.ScopeUid && m.Name == createdCover.Name);

        media.OriginalData = originalBytes;
        media.OriginalExtension = ".png";
        media.OriginalMimeType = "image/png";
        media.OriginalName = "original-cover.png";
        media.MimeType = "image/avif";
        media.Extension = ".avif";
        media.Name = "optimized-cover.avif";

        await dbContext.SaveChangesAsync();

        var editRequest = new CoverImageEditRequest
        {
            CoverImageUrl = createdCover.Location,
            ContentTitle = "Edit Using Original",
            ContentDescription = "Ensure edits send original image data.",
            Prompt = "Add a subtle highlight",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover/edit", editRequest, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.OriginalName.Should().NotBeNullOrWhiteSpace();
        response.OriginalExtension.Should().NotBeNullOrWhiteSpace();
        response.OriginalMimeType.Should().NotBeNullOrWhiteSpace();
        response.OriginalSize.Should().NotBeNull();
        response.Width.Should().NotBeNull();
        response.Height.Should().NotBeNull();
        response.OriginalWidth.Should().NotBeNull();
        response.OriginalHeight.Should().NotBeNull();

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.EditImage.Should().NotBeNull();
        lastRequest.EditImage!.FileName.Should().Be("original-cover.png");
        lastRequest.EditImage.MimeType.Should().Be("image/png");
    }

    [Fact]
    public async Task CoverImageGeneration_WithOptimisationEnabled_UsesPreferredFormatAndCoverDimensions()
    {
        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "300x150");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");

        var request = new CoverImageGenerationRequest
        {
            ContentTitle = "Media Settings Optimized",
            ContentDescription = "Cover generation should use preferred format and cover dimensions.",
            ContentSlug = "ai-cover-settings-optimized",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover", request, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.Name.Should().EndWith(".webp");
        response.Extension.Should().Be(".webp");
        response.MimeType.Should().Be("image/webp");

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.Width.Should().Be(300);
        lastRequest.Height.Should().Be(150);

        var mediaBytes = await GetMediaBytesAsync(response.Location);
        using var image = new MagickImage(mediaBytes);
        image.Width.Should().Be(300);
        image.Height.Should().Be(150);
    }

    [Fact]
    public async Task CoverImageGeneration_WithOptimisationDisabled_UsesOriginalFormatAndCoverDimensions()
    {
        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "400x200");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");

        var request = new CoverImageGenerationRequest
        {
            ContentTitle = "Media Settings Original",
            ContentDescription = "Cover generation should keep original format when optimisation is disabled.",
            ContentSlug = "ai-cover-settings-original",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover", request, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.Name.Should().EndWith(".png");
        response.Extension.Should().Be(".png");
        response.MimeType.Should().Be("image/png");

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.Width.Should().Be(400);
        lastRequest.Height.Should().Be(200);

        var mediaBytes = await GetMediaBytesAsync(response.Location);
        using var image = new MagickImage(mediaBytes);
        image.Width.Should().Be(400);
        image.Height.Should().Be(200);
    }

    [Fact]
    public async Task CoverImageEdit_WithOptimisationDisabled_UsesOriginalFormatAndUpdatedCoverDimensions()
    {
        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "240x120");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "webp");
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "true");

        var createRequest = new CoverImageGenerationRequest
        {
            ContentTitle = "Media Edit Base",
            ContentDescription = "Initial cover image for edit tests.",
            ContentSlug = "ai-cover-edit-settings",
        };

        var created = await PostTest<MediaDetailsDto>("/api/content/ai-cover", createRequest, HttpStatusCode.OK);
        created.Should().NotBeNull();

        await SetSystemSettingAsync(SettingKeys.MediaCoverDimensions, "320x160");
        await SetSystemSettingAsync(SettingKeys.MediaPreferredFormat, "avif");
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");

        var editRequest = new CoverImageEditRequest
        {
            CoverImageUrl = created!.Location,
            ContentTitle = "Media Edit Updated",
            ContentDescription = "Updated cover image after toggling optimisation.",
            Prompt = "Add subtle highlights",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover/edit", editRequest, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.Name.Should().EndWith(".png");
        response.Extension.Should().Be(".png");
        response.MimeType.Should().Be("image/png");

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.Width.Should().Be(320);
        lastRequest.Height.Should().Be(160);
        lastRequest.EditImage.Should().NotBeNull();
        lastRequest.EditImage!.FileName.Should().EndWith(".png");
        lastRequest.EditImage.MimeType.Should().Be("image/png");

        var mediaBytes = await GetMediaBytesAsync(response.Location);
        using var image = new MagickImage(mediaBytes);
        image.Width.Should().Be(320);
        image.Height.Should().Be(160);
    }

    [Fact]
    public async Task CoverImageGeneration_WithSampleImagePaths_IncludesSamplesInProviderRequest()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        var sampleBytes = LoadEmbeddedResource("cover-sample.png");
        var sampleMedia = await UploadMediaAsync(sampleBytes, "sample-cover.png", "blog/article/some-name");
        var dbContext = App.GetDbContext();
        var sampleEntity = dbContext!.Media!
            .First(m => m.ScopeUid == sampleMedia.ScopeUid && m.Name == sampleMedia.Name);
        sampleEntity.OriginalData = sampleBytes;
        sampleEntity.OriginalExtension = ".png";
        sampleEntity.OriginalMimeType = "image/png";
        sampleEntity.OriginalName = "sample-cover-original.png";
        sampleEntity.Data = new byte[] { 1, 2, 3, 4 };
        sampleEntity.Extension = ".avif";
        sampleEntity.MimeType = "image/avif";
        await dbContext.SaveChangesAsync();

        var request = new CoverImageGenerationRequest
        {
            ContentTitle = "Sample Image Coverage",
            ContentDescription = "Ensure sample image paths are passed to the provider.",
            ContentSlug = "ai-cover-sample-images",
            SampleImagePaths = new List<string>
            {
                $"{sampleMedia.ScopeUid}/{sampleMedia.Name}",
            },
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover", request, HttpStatusCode.OK);

        response.Should().NotBeNull();

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.Prompt.Should().Contain(sampleEntity.OriginalName!);
        lastRequest!.SampleImages.Should().HaveCount(1);
        lastRequest.SampleImages[0].FileName.Should().Be(sampleEntity.OriginalName);
        lastRequest.SampleImages[0].MimeType.Should().Be(sampleEntity.OriginalMimeType);
        lastRequest.SampleImages[0].Data.Should().BeEquivalentTo(sampleBytes);
    }

    [Fact]
    public async Task CoverImageGeneration_WithoutSampleImagePaths_UsesRecentCoverImages()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        TrackEntityType<Content>();

        var sampleBytes = LoadEmbeddedResource("cover-sample.png");
        var sampleMedia = await UploadMediaAsync(sampleBytes, "recent-cover.png", "recent-cover-scope");

        var contentCreate = new ContentCreateDto
        {
            Title = "Recent Cover Content",
            Description = "This content references a cover image for sampling.",
            Body = "Body for recent cover content.",
            Slug = "recent-cover-content",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{sampleMedia.ScopeUid}/{sampleMedia.Name}",
        };

        await PostTest("/api/content", contentCreate, HttpStatusCode.Created);

        var request = new CoverImageGenerationRequest
        {
            ContentTitle = "Auto Samples",
            ContentDescription = "No sample paths provided, should use recent covers.",
            ContentSlug = "ai-cover-auto-samples",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover", request, HttpStatusCode.OK);

        response.Should().NotBeNull();

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.SampleImages.Should().NotBeEmpty();
        lastRequest.SampleImages.Should().Contain(s => s.FileName == sampleMedia.Name);
        lastRequest.Prompt.Should().Contain(sampleMedia.Name);
    }

    [Fact]
    public async Task CoverImageEdit_WithSampleImagePaths_IncludesSamplesInProviderRequest()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        var coverBytes = LoadEmbeddedResource("cover-sample.png");
        var sampleBytes = LoadEmbeddedResource("cover-sample.png");

        var createdCover = await UploadMediaAsync(coverBytes, "edit-cover.png", "blog/article/some-name");
        var sampleMedia = await UploadMediaAsync(sampleBytes, "sample-edit.png", "blog/article/some-name");

        var editRequest = new CoverImageEditRequest
        {
            CoverImageUrl = createdCover.Location,
            ContentTitle = "Edit With Samples",
            ContentDescription = "Editing cover image while providing samples.",
            Prompt = "Add soft lighting",
            SampleImagePaths = new List<string>
            {
                $"/api/media/{sampleMedia.ScopeUid}/{sampleMedia.Name}",
            },
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover/edit", editRequest, HttpStatusCode.OK);

        response.Should().NotBeNull();

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.EditImage.Should().NotBeNull();
        lastRequest.Prompt.Should().Contain(editRequest.Prompt);
        lastRequest.Prompt.Should().Contain(sampleMedia.Name);
        lastRequest.SampleImages.Should().HaveCount(1);
        lastRequest.SampleImages[0].FileName.Should().Be(sampleMedia.Name);
        lastRequest.SampleImages[0].MimeType.Should().Be(sampleMedia.MimeType);
        lastRequest.SampleImages[0].Data.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CoverImageEdit_WithoutSampleImagePaths_DoesNotUseRecentCoverImages()
    {
        await SetSystemSettingAsync(SettingKeys.MediaEnableOptimisation, "false");
        await SetSystemSettingAsync(SettingKeys.MediaMaxDimensions, "5000x5000");

        TrackEntityType<Content>();

        var sampleBytes = LoadEmbeddedResource("cover-sample.png");
        var recentMedia = await UploadMediaAsync(sampleBytes, "recent-cover.png", "recent-cover-scope");

        var contentCreate = new ContentCreateDto
        {
            Title = "Recent Cover Content",
            Description = "This content references a cover image for sampling.",
            Body = "Body for recent cover content.",
            Slug = "recent-cover-content-edit",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{recentMedia.ScopeUid}/{recentMedia.Name}",
        };

        await PostTest("/api/content", contentCreate, HttpStatusCode.Created);

        var coverBytes = LoadEmbeddedResource("cover-sample.png");
        var createdCover = await UploadMediaAsync(coverBytes, "edit-cover.png", "blog/article/edit-no-samples");

        var editRequest = new CoverImageEditRequest
        {
            CoverImageUrl = createdCover.Location,
            ContentTitle = "Edit Without Samples",
            ContentDescription = "Editing cover image without samples.",
            Prompt = "Add subtle highlights",
        };

        var response = await PostTest<MediaDetailsDto>("/api/content/ai-cover/edit", editRequest, HttpStatusCode.OK);

        response.Should().NotBeNull();

        var lastRequest = TestAIProviderService.GetLastImageRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.EditImage.Should().NotBeNull();
        lastRequest.SampleImages.Should().BeEmpty();
        lastRequest.Prompt.Should().NotContain(recentMedia.Name);
        lastRequest.Prompt.Should().Be(editRequest.Prompt);
    }

    [Fact]
    public async Task ContentDraftEndpoint_ShouldUseProviderAndReturnDraft()
    {
        TestAIProviderService.EnqueueTextResponse(@"{
  ""title"": ""AI Title"",
  ""description"": ""This is a long enough description for testing content generation."",
  ""body"": ""Generated body for tests."",
  ""slug"": ""ai-title"",
  ""author"": ""Test Author"",
  ""category"": ""Product"",
  ""tags"": [""Tag1""],
  ""coverImageAlt"": ""Cover alt""
}");

        TrackEntityType<Content>();
        await PostTest("/api/content", new TestContent("-ai"));

        var request = new ContentGenerationRequest
        {
            Language = "en",
            ContentType = "blog-post",
            Prompt = "Write about automated testing",
            WordCount = 50,
        };

        var response = await PostTest<ContentDetailsDto>("/api/content/ai-draft", request, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.Title.Should().Be("AI Title");
        response.Description.Should().Contain("long enough description");

        var lastRequest = TestAIProviderService.GetLastTextRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.UserPrompt.Should().Contain(request.Prompt);
        lastRequest.UserPrompt.Should().Contain("Generate new content in en");
    }

    [Fact]
    public async Task ContentEditEndpoint_ShouldUseProviderAndReturnEdits()
    {
        TestAIProviderService.EnqueueTextResponse(@"{
  ""title"": ""Edited Title"",
  ""description"": ""Edited description that is long enough for validation."",
  ""body"": ""Edited body"",
  ""slug"": ""edited-slug"",
  ""tags"": [""TagA""],
  ""category"": ""Edited""
}");

        var request = new ContentEditRequest
        {
            Prompt = "Shorten the content",
            Title = "Original Title",
            Description = "Original description that is long enough for validation.",
            Body = "Original body",
            Slug = "original-slug",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Original",
            Tags = new[] { "OriginalTag" },
            AllowComments = true,
        };

        var response = await PostTest<ContentDetailsDto>("/api/content/ai-edit", request, HttpStatusCode.OK);

        response.Should().NotBeNull();
        response!.Title.Should().Be("Edited Title");
        response.Description.Should().Contain("Edited description");
        response.Slug.Should().Be("edited-slug");

        var lastRequest = TestAIProviderService.GetLastTextRequest();
        lastRequest.Should().NotBeNull();
        lastRequest!.UserPrompt.Should().Contain(request.Prompt);
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

    private async Task SetSystemSettingAsync(string key, string value)
    {
        var url = $"/api/settings/system/{Uri.EscapeDataString(key)}?value={Uri.EscapeDataString(value)}";
        var response = await Request(HttpMethod.Put, url, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<byte[]> GetMediaBytesAsync(string location)
    {
        var response = await GetTest(location, HttpStatusCode.OK);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<MediaDetailsDto> UploadMediaAsync(byte[] bytes, string fileName, string scopeUid)
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.TryGetContentType(fileName, out var contentType);

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
        media!.Location.Should().NotBeNullOrWhiteSpace();

        return media!;
    }
}
