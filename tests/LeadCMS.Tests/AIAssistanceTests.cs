// <copyright file="AIAssistanceTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Tests.TestServices;

namespace LeadCMS.Tests;

[Collection("AI Assistance Tests")]
public class AIAssistanceTests : BaseTestAutoLogin
{
    public AIAssistanceTests()
    {
        TestAIProviderService.Reset();
    }

    [Fact]
    public async Task CoverImageGenerationEndpoint_ShouldGenerateCoverImage()
    {
        var request = new CoverImageGenerationRequest
        {
            ContentTitle = "Automated Testing in Practice",
            ContentDescription = "A practical guide to test automation strategies and tooling choices.",
            ContentSlug = "ai-cover-test",
            Prompt = "Use a modern abstract style",
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
        lastRequest!.Prompt.Should().Contain(editRequest.Prompt);
        lastRequest.EditImage.Should().NotBeNull();
        lastRequest.EditImage!.FileName.Should().EndWith(".png");
        lastRequest.EditImage.MimeType.Should().Be("image/png");
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

        await EnsureContentTypeExists("blog-post");
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

        await EnsureContentTypeExists("blog-post");
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

    private async Task EnsureContentTypeExists(string uid)
    {
        TrackEntityType<ContentType>();

        var existing = await GetTest<List<ContentTypeDetailsDto>>("/api/content-types", HttpStatusCode.OK);
        if (existing != null && existing.Exists(t => t.Uid == uid))
        {
            return;
        }

        var createRequest = new ContentTypeCreateDto
        {
            Uid = uid,
            Format = ContentFormat.MD,
            SupportsComments = true,
            SupportsCoverImage = true,
        };

        await PostTest("/api/content-types", createRequest, HttpStatusCode.Created);
    }
}
