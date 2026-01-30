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
        var mediaUnused = await CreateMediaAsync("usage-unused.png");

        var bodyOne = "<Image src=\"/api/media/" + mediaOne.ScopeUid + "/" + mediaOne.Name + "\" alt=\"First\" />\n" +
                  "<img src=\"/api/media/" + mediaTwo.ScopeUid + "/" + mediaTwo.Name + "\" alt=\"Second\" />\n" +
                  "![Again](/api/media/" + mediaOne.ScopeUid + "/" + mediaOne.Name + ")";
        await CreateContentWithBodyAsync(bodyOne, "-usage-1");

        var bodyTwo = $"<img src=\"/api/media/{mediaOne.ScopeUid}/{mediaOne.Name}\" alt=\"Third\" />";
        await CreateContentWithBodyAsync(bodyTwo, "-usage-2");

        await ExecuteMediaMetaUpdateTaskAsync();

        var refreshedOne = await GetMediaByNameAsync(mediaOne.ScopeUid, mediaOne.Name);
        var refreshedTwo = await GetMediaByNameAsync(mediaTwo.ScopeUid, mediaTwo.Name);
        var refreshedUnused = await GetMediaByNameAsync(mediaUnused.ScopeUid, mediaUnused.Name);

        refreshedOne.UsageCount.Should().Be(3);
        refreshedTwo.UsageCount.Should().Be(1);
        refreshedUnused.UsageCount.Should().Be(0);
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

    private async Task CreateContentWithBodyAsync(string body, string suffix)
    {
        var content = new TestContent(suffix)
        {
            Body = body,
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
}
