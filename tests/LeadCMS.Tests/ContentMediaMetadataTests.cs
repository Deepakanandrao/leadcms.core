// <copyright file="ContentMediaMetadataTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Tests;

public class ContentMediaMetadataTests : BaseTestAutoLogin
{
    public ContentMediaMetadataTests()
        : base()
    {
        TrackEntityType<Media>();
        TrackEntityType<Content>();
    }

    [Fact]
    public async Task CreateContent_WithHtmlImageTag_ShouldUpdateMediaDescription()
    {
        var createdMedia = await CreateMediaAsync("html-image.png");

        var body = $"<p>Test</p><img src=\"/api/media/{createdMedia.ScopeUid}/{createdMedia.Name}\" alt=\"HTML alt text\" />";
        await CreateContentWithBodyAsync(body, "-html-img");

        var media = await GetMediaByNameAsync(createdMedia.ScopeUid, createdMedia.Name);
        media.Description.Should().Be("HTML alt text");
    }

    [Fact]
    public async Task CreateContent_WithMarkdownImage_ShouldUpdateMediaDescription()
    {
        var createdMedia = await CreateMediaAsync("markdown-image.png");

        var body = $"![Markdown alt text](/api/media/{createdMedia.ScopeUid}/{createdMedia.Name})";
        await CreateContentWithBodyAsync(body, "-markdown-img");

        var media = await GetMediaByNameAsync(createdMedia.ScopeUid, createdMedia.Name);
        media.Description.Should().Be("Markdown alt text");
    }

    [Fact]
    public async Task CreateContent_WithMdxImageTag_Multiline_ShouldUpdateMediaDescription()
    {
        var createdMedia = await CreateMediaAsync("mdx-image.png");

        var body = "<Image\n" +
                   $"  src=\"/api/media/{createdMedia.ScopeUid}/{createdMedia.Name}\"\n" +
                   "  caption=\"MDX caption text\"\n" +
                   "/>";
        await CreateContentWithBodyAsync(body, "-mdx-img");

        var media = await GetMediaByNameAsync(createdMedia.ScopeUid, createdMedia.Name);
        media.Description.Should().Be("MDX caption text");
    }

    [Fact]
    public async Task CreateContent_WithAllImageTypes_ShouldUpdateAllMediaDescriptions()
    {
        var htmlMedia = await CreateMediaAsync("all-html.png");
        var mdxMedia = await CreateMediaAsync("all-mdx.png");
        var markdownMedia = await CreateMediaAsync("all-md.png");

        var body = $@"<Image src=""/api/media/{mdxMedia.ScopeUid}/{mdxMedia.Name}"" alt=""MDX alt text"" />
    <p>middle</p>
    <img src=""/api/media/{htmlMedia.ScopeUid}/{htmlMedia.Name}"" alt=""HTML alt text"" />
    ![Markdown alt text](/api/media/{markdownMedia.ScopeUid}/{markdownMedia.Name})";

        await CreateContentWithBodyAsync(body, "-all-img");

        var html = await GetMediaByNameAsync(htmlMedia.ScopeUid, htmlMedia.Name);
        var mdx = await GetMediaByNameAsync(mdxMedia.ScopeUid, mdxMedia.Name);
        var markdown = await GetMediaByNameAsync(markdownMedia.ScopeUid, markdownMedia.Name);

        html.Description.Should().Be("HTML alt text");
        mdx.Description.Should().Be("MDX alt text");
        markdown.Description.Should().Be("Markdown alt text");
    }

    [Fact]
    public async Task CreateContent_WithCoverImage_ShouldUpdateCoverMediaMetadata()
    {
        var coverMedia = await CreateMediaAsync("cover-media.png");

        var content = new ContentCreateDto
        {
            Title = "Cover Title",
            Description = "Cover description long enough",
            Body = "Body for cover metadata test.",
            Slug = "cover-metadata-test",
            Type = "blog-post",
            Author = "Tester",
            Language = "en",
            Category = "Product",
            Tags = new[] { "Tag1" },
            AllowComments = true,
            CoverImageUrl = $"/api/media/{coverMedia.ScopeUid}/{coverMedia.Name}",
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content);
        createdContent.Should().NotBeNull();

        var media = await GetMediaByNameAsync(coverMedia.ScopeUid, coverMedia.Name);
        media.Description.Should().Be(content.Title);
        media.Tags.Should().Contain(tag => string.Equals(tag, "cover", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteMediaMetaUpdateTask_ShouldUpdateUsageCountAcrossAllContent()
    {
        var mediaOne = await CreateMediaAsync("usage-one.png");
        var mediaTwo = await CreateMediaAsync("usage-two.png");
        var mediaCover = await CreateMediaAsync("usage-cover.png");
        var mediaUnused = await CreateMediaAsync("usage-unused.png");

        var bodyOne = "<Image src=\"/api/media/" + mediaOne.ScopeUid + "/" + mediaOne.Name + "\" alt=\"First\" />\n" +
                  "<img src=\"/api/media/" + mediaTwo.ScopeUid + "/" + mediaTwo.Name + "\" alt=\"Second\" />\n" +
                  "![Again](/api/media/" + mediaOne.ScopeUid + "/" + mediaOne.Name + ")";
        await CreateContentWithBodyAsync(bodyOne, "-usage-1");

        var bodyTwo = $"<img src=\"/api/media/{mediaOne.ScopeUid}/{mediaOne.Name}\" alt=\"Third\" />";
        await CreateContentWithCoverImageAsync(bodyTwo, $"/api/media/{mediaCover.ScopeUid}/{mediaCover.Name}", "-usage-2");

        // Another content using the same cover image
        await CreateContentWithCoverImageAsync("Simple body", $"/api/media/{mediaCover.ScopeUid}/{mediaCover.Name}", "-usage-3");

        await ExecuteMediaMetaUpdateTaskAsync();

        var refreshedOne = await GetMediaByNameAsync(mediaOne.ScopeUid, mediaOne.Name);
        var refreshedTwo = await GetMediaByNameAsync(mediaTwo.ScopeUid, mediaTwo.Name);
        var refreshedCover = await GetMediaByNameAsync(mediaCover.ScopeUid, mediaCover.Name);
        var refreshedUnused = await GetMediaByNameAsync(mediaUnused.ScopeUid, mediaUnused.Name);

        refreshedOne.UsageCount.Should().Be(3);
        refreshedTwo.UsageCount.Should().Be(1);
        refreshedCover.UsageCount.Should().Be(2); // Used as cover image in 2 content items
        refreshedUnused.UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateContent_WithHtmlImage_ShouldTagMediaWithContentType()
    {
        var createdMedia = await CreateMediaAsync("ct-tag-html.png");

        var body = $"<img src=\"/api/media/{createdMedia.ScopeUid}/{createdMedia.Name}\" alt=\"Tagged image\" />";
        await CreateContentWithBodyAsync(body, "-ct-tag-html", "blog-post");

        var media = await GetMediaByNameAsync(createdMedia.ScopeUid, createdMedia.Name);
        media.Tags.Should().Contain("blog-post");
    }

    [Fact]
    public async Task CreateContent_WithDifferentTypes_ShouldAccumulateTags()
    {
        await CreateContentTypeAsync("landing");
        var createdMedia = await CreateMediaAsync("ct-multi-tag.png");

        var body = $"<img src=\"/api/media/{createdMedia.ScopeUid}/{createdMedia.Name}\" alt=\"Multi-tagged\" />";
        await CreateContentWithBodyAsync(body, "-ct-multi-1", "blog-post");
        await CreateContentWithBodyAsync(body, "-ct-multi-2", "landing");

        var media = await GetMediaByNameAsync(createdMedia.ScopeUid, createdMedia.Name);
        media.Tags.Should().Contain("blog-post");
        media.Tags.Should().Contain("landing");
    }

    [Fact]
    public async Task CreateContent_ShouldPreserveExistingTags()
    {
        var createdMedia = await CreateMediaAsync("ct-preserve.png");

        // First, create content with cover image to get the "cover" tag added
        await CreateContentWithCoverImageAsync(
            "Body text",
            $"/api/media/{createdMedia.ScopeUid}/{createdMedia.Name}",
            "-ct-preserve");

        var mediaAfterCover = await GetMediaByNameAsync(createdMedia.ScopeUid, createdMedia.Name);
        mediaAfterCover.Tags.Should().Contain(tag => string.Equals(tag, "cover", StringComparison.OrdinalIgnoreCase));

        // Now create another content using this media in body
        var body = $"<img src=\"/api/media/{createdMedia.ScopeUid}/{createdMedia.Name}\" alt=\"Preserve test\" />";
        await CreateContentWithBodyAsync(body, "-ct-preserve-2", "blog-post");

        var media = await GetMediaByNameAsync(createdMedia.ScopeUid, createdMedia.Name);
        media.Tags.Should().Contain(tag => string.Equals(tag, "cover", StringComparison.OrdinalIgnoreCase));
        media.Tags.Should().Contain("blog-post");
    }

    [Fact]
    public async Task ExecuteMediaMetaUpdateTask_ShouldAddContentTypeTags()
    {
        await CreateContentTypeAsync("landing");
        var mediaOne = await CreateMediaAsync("ct-task-one.png");
        var mediaTwo = await CreateMediaAsync("ct-task-two.png");

        var bodyOne = $"<img src=\"/api/media/{mediaOne.ScopeUid}/{mediaOne.Name}\" alt=\"Task test one\" />";
        await CreateContentWithBodyAsync(bodyOne, "-ct-task-1", "blog-post");

        var bodyTwo = $"<img src=\"/api/media/{mediaTwo.ScopeUid}/{mediaTwo.Name}\" alt=\"Task test two\" />" +
                      $"<img src=\"/api/media/{mediaOne.ScopeUid}/{mediaOne.Name}\" alt=\"Task test one again\" />";
        await CreateContentWithBodyAsync(bodyTwo, "-ct-task-2", "landing");

        await ExecuteMediaMetaUpdateTaskAsync();

        var refreshedOne = await GetMediaByNameAsync(mediaOne.ScopeUid, mediaOne.Name);
        var refreshedTwo = await GetMediaByNameAsync(mediaTwo.ScopeUid, mediaTwo.Name);

        refreshedOne.Tags.Should().Contain("blog-post");
        refreshedOne.Tags.Should().Contain("landing");
        refreshedTwo.Tags.Should().Contain("landing");
    }

    protected override Task<HttpResponseMessage> Request(HttpMethod method, string url, object? payload)
    {
        if (payload is not TestMedia)
        {
            return base.Request(method, url, payload);
        }

        var request = new HttpRequestMessage(method, url);
        var testMedia = (TestMedia)payload!;
        var content = new MultipartFormDataContent
        {
            { new StreamContent(testMedia.DataBuffer), "File", testMedia.File!.Name },
            { new StringContent(testMedia.ScopeUid), "ScopeUid" },
        };

        request.Content = content;
        request.Headers.Authorization = GetAuthenticationHeaderValue();

        return client.SendAsync(request);
    }

    private async Task<MediaDetailsDto> CreateMediaAsync(string fileName)
    {
        var testMedia = new TestMedia(fileName, 1024);
        var createdMedia = await PostTest<MediaDetailsDto>("/api/media", testMedia);
        createdMedia.Should().NotBeNull();
        createdMedia!.Description.Should().BeNullOrEmpty();
        return createdMedia;
    }

    private async Task CreateContentWithBodyAsync(string body, string suffix, string contentType = "blog-post")
    {
        var content = new TestContent(suffix)
        {
            Body = body,
            Type = contentType,
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content);
        createdContent.Should().NotBeNull();
    }

    private async Task CreateContentWithCoverImageAsync(string body, string coverImageUrl, string suffix)
    {
        var content = new TestContent(suffix)
        {
            Body = body,
            CoverImageUrl = coverImageUrl,
        };

        var createdContent = await PostTest<ContentDetailsDto>("/api/content", content);
        createdContent.Should().NotBeNull();
    }

    private async Task<MediaDetailsDto> GetMediaByNameAsync(string scopeUid, string name)
    {
        var mediaList = await GetTest<List<MediaDetailsDto>>($"/api/media?filter[where][scopeUid][eq]={scopeUid}&filter[where][name][eq]={name}");
        mediaList.Should().NotBeNull();
        mediaList!.Count.Should().Be(1);
        return mediaList[0];
    }

    private async Task ExecuteMediaMetaUpdateTaskAsync()
    {
        var response = await GetRequest("/api/tasks/execute/MediaMetaUpdateTask");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task CreateContentTypeAsync(string uid)
    {
        // Check if content type already exists
        var existing = await GetTest<List<ContentTypeDetailsDto>>($"/api/content-types?filter[where][uid][eq]={uid}");
        if (existing != null && existing.Count > 0)
        {
            return;
        }

        var contentType = new ContentTypeCreateDto
        {
            Uid = uid,
            Format = ContentFormat.MD,
            SupportsComments = true,
            SupportsCoverImage = true,
        };

        var result = await PostTest<ContentTypeDetailsDto>("/api/content-types", contentType);
        result.Should().NotBeNull();
    }
}
