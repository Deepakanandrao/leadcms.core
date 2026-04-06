// <copyright file="ContentTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Tests;

public class ContentTests : SimpleTableTests<Content, TestContent, ContentUpdateDto, IEntityService<Content>>
{
    private const string ContentTypesApi = "/api/content-types";

    public ContentTests()
        : base("/api/content")
    {
    }

    [Fact]
    public async Task GetAllTestAnonymous()
    {
        await GetAllRecords(true);
    }

    [Fact]
    public async Task CreateAndGetItemTestAnonymous()
    {
        await CreateAndGetItem(true);
    }

    [Fact]
    public async Task CheckTags()
    {
        await CreateItem();
        var response = await GetTest(itemsUrl + "/tags", HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var data = JsonHelper.Deserialize<string[]>(content);
        data.Should().NotBeNull();
        data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CheckCategories()
    {
        await CreateItem();
        var response = await GetTest(itemsUrl + "/categories", HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var data = JsonHelper.Deserialize<string[]>(content);
        data.Should().NotBeNull();
        data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CheckAuthors()
    {
        await CreateItem();
        var response = await GetTest(itemsUrl + "/authors", HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var data = JsonHelper.Deserialize<string[]>(content);
        data.Should().NotBeNull();
        data.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateContent_WithDuplicateSlugAndLanguage_ShouldReturnMeaningfulConflictError()
    {
        var uid = Guid.NewGuid().ToString("N");
        var slug = $"duplicate-slug-{uid}";

        var firstContent = new TestContent(uid)
        {
            Slug = slug,
            Language = "ru",
        };

        await PostTest(itemsUrl, firstContent, HttpStatusCode.Created);

        var duplicateContent = new TestContent(uid + "-dup")
        {
            Slug = slug,
            Language = "ru",
        };

        var response = await Request(HttpMethod.Post, itemsUrl, duplicateContent);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(responseContent);

        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be((int)HttpStatusCode.UnprocessableEntity);
        problemDetails.Title.Should().NotBeNullOrWhiteSpace();
        problemDetails.Title.Should().NotContain("duplicate key value violates unique constraint");
        problemDetails.Title.Should().NotContain("ix_content_slug_language");
    }

    [Fact]
    public async Task PutContentWithNullValues_ShouldReplaceCompleteEntity()
    {
        // First create an item with non-null values
        var publishedContent = new TestContent
        {
            PublishedAt = DateTime.UtcNow,
            Seo = new SeoMetadataDto
            {
                MetaTitle = "Original SEO title",
                MetaDescription = "Original SEO description",
                CanonicalUrl = "https://example.com/original",
                OpenGraphTitle = "Original OG title",
                OpenGraphDescription = "Original OG description",
                OpenGraphImageUrl = "/api/images/original-og.png",
                Robots = "index,follow",
                Keywords = new[] { "original", "seo" },
            },
        };

        var contentPath = await PostTest(itemsUrl, publishedContent, HttpStatusCode.Created);

        // Get the created item to verify initial values
        var getResponse = await GetTest<ContentDetailsDto>(contentPath, HttpStatusCode.OK);
        getResponse.Should().NotBeNull();
        getResponse!.Category.Should().NotBeNullOrEmpty();
        getResponse.Source.Should().BeNull(); // Source should be null initially
        getResponse.PublishedAt.Should().NotBeNull();

        // Now create a PUT request with null values for optional fields
        var putDto = new ContentCreateDto
        {
            Title = "Updated Title",
            Description = "Updated Description with min 20 charters",
            Body = "Updated Body",
            Slug = getResponse.Slug, // Keep the same slug
            Type = getResponse.Type, // Keep the same type
            Author = "Updated Author",
            Language = getResponse.Language, // Keep the same language
            TranslationKey = null, // Set to null
            Category = string.Empty, // Set to empty string (which should be saved as empty)
            Tags = Array.Empty<string>(),
            AllowComments = false,
            Seo = null,
            Source = null, // Set to null
            PublishedAt = null, // Set to null
        };

        // Execute PUT request
        var putResponse = await Request(HttpMethod.Put, $"{itemsUrl}/{getResponse.Id}", putDto);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var putContent = await putResponse.Content.ReadAsStringAsync();
        var updatedItem = JsonHelper.Deserialize<ContentDetailsDto>(putContent);

        // Verify that all values were replaced, including nulls
        updatedItem.Should().NotBeNull();
        updatedItem!.Title.Should().Be("Updated Title");
        updatedItem.Description.Should().Be("Updated Description with min 20 charters");
        updatedItem.Body.Should().Be("Updated Body");
        updatedItem.Author.Should().Be("Updated Author");
        updatedItem.Category.Should().Be(string.Empty); // Should be empty string, not the original category
        updatedItem.TranslationKey.Should().BeNull(); // Should be null
        updatedItem.Source.Should().BeNull(); // Should be null
        updatedItem.PublishedAt.Should().BeNull(); // Should be null
        updatedItem.AllowComments.Should().BeFalse();
        updatedItem.Seo.Should().BeNull();
        updatedItem.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ContentType_ShouldPersistSupportsSEO()
    {
        var uid = $"seo-type-{Guid.NewGuid():N}";

        var contentType = await EnsureContentTypeAsync(uid, supportsPreviewSlug: false, supportsSeo: true);

        contentType.Should().NotBeNull();
        contentType!.SupportsSEO.Should().BeTrue();

        var fetched = await GetTest<ContentTypeDetailsDto>($"{ContentTypesApi}/{contentType.Id}", HttpStatusCode.OK);
        fetched.Should().NotBeNull();
        fetched!.SupportsSEO.Should().BeTrue();
    }

    [Fact]
    public async Task CreateContent_WithSeoMetadata_ShouldPersistForSeoEnabledType()
    {
        var typeUid = $"seo-enabled-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, supportsPreviewSlug: false, supportsSeo: true);

        var seo = new SeoMetadataDto
        {
            MetaTitle = "SEO Page Title",
            MetaDescription = "SEO description that is long enough for a realistic metadata payload.",
            CanonicalUrl = "https://example.com/blog/seo-page-title",
            OpenGraphTitle = "SEO OG Title",
            OpenGraphDescription = "SEO OG description",
            OpenGraphImageUrl = "/api/images/seo-og.png",
            Robots = "index,follow",
            Keywords = new[] { "seo", "metadata", "html" },
        };

        var content = new TestContent(Guid.NewGuid().ToString("N"))
        {
            Type = typeUid,
            Seo = seo,
        };

        var created = await PostTest<ContentDetailsDto>(itemsUrl, content, HttpStatusCode.Created);

        created.Should().NotBeNull();
        created!.Seo.Should().NotBeNull();
        created.Seo!.MetaTitle.Should().Be(seo.MetaTitle);
        created.Seo.MetaDescription.Should().Be(seo.MetaDescription);
        created.Seo.CanonicalUrl.Should().Be(seo.CanonicalUrl);
        created.Seo.OpenGraphTitle.Should().Be(seo.OpenGraphTitle);
        created.Seo.OpenGraphDescription.Should().Be(seo.OpenGraphDescription);
        created.Seo.OpenGraphImageUrl.Should().Be(seo.OpenGraphImageUrl);
        created.Seo.Robots.Should().Be(seo.Robots);
        created.Seo.Keywords.Should().Equal(seo.Keywords);

        var fetched = await GetTest<ContentDetailsDto>($"{itemsUrl}/{created.Id}", HttpStatusCode.OK);
        fetched.Should().NotBeNull();
        fetched!.Seo.Should().NotBeNull();
        fetched.Seo!.MetaTitle.Should().Be(seo.MetaTitle);
        fetched.Seo.Keywords.Should().Equal(seo.Keywords);
    }

    [Fact]
    public async Task PutContent_WithSeoMetadata_ShouldPersistForSeoEnabledType()
    {
        var typeUid = $"seo-put-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, supportsPreviewSlug: false, supportsSeo: true);

        var created = await PostTest<ContentDetailsDto>(
            itemsUrl,
            new TestContent(Guid.NewGuid().ToString("N"))
            {
                Type = typeUid,
            },
            HttpStatusCode.Created);

        created.Should().NotBeNull();

        var seo = new SeoMetadataDto
        {
            MetaTitle = "Updated SEO title",
            MetaDescription = "Updated SEO description that is long enough to look realistic.",
            CanonicalUrl = "https://example.com/blog/updated-seo-title",
            OpenGraphTitle = "Updated SEO OG Title",
            OpenGraphDescription = "Updated SEO OG Description",
            OpenGraphImageUrl = "/api/images/updated-seo-og.png",
            Robots = "index,follow",
            Keywords = new[] { "updated", "seo", "metadata" },
        };

        var putDto = new ContentCreateDto
        {
            Title = created!.Title,
            Description = created.Description,
            Body = created.Body,
            CoverImageUrl = created.CoverImageUrl,
            CoverImageAlt = created.CoverImageAlt,
            Slug = created.Slug,
            PreviewSlug = created.PreviewSlug,
            Seo = seo,
            Type = created.Type,
            Author = created.Author,
            Language = created.Language,
            TranslationKey = created.TranslationKey,
            Category = created.Category,
            Tags = created.Tags,
            AllowComments = created.AllowComments,
            Source = created.Source,
            PublishedAt = created.PublishedAt,
        };

        var putResponse = await Request(HttpMethod.Put, $"{itemsUrl}/{created.Id}", putDto);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedContent = await putResponse.Content.ReadAsStringAsync();
        var updated = JsonHelper.Deserialize<ContentDetailsDto>(updatedContent);

        updated.Should().NotBeNull();
        updated!.Seo.Should().NotBeNull();
        updated.Seo!.MetaTitle.Should().Be(seo.MetaTitle);
        updated.Seo.MetaDescription.Should().Be(seo.MetaDescription);
        updated.Seo.CanonicalUrl.Should().Be(seo.CanonicalUrl);
        updated.Seo.OpenGraphTitle.Should().Be(seo.OpenGraphTitle);
        updated.Seo.OpenGraphDescription.Should().Be(seo.OpenGraphDescription);
        updated.Seo.OpenGraphImageUrl.Should().Be(seo.OpenGraphImageUrl);
        updated.Seo.Robots.Should().Be(seo.Robots);
        updated.Seo.Keywords.Should().Equal(seo.Keywords);

        // Verify data survives a database round-trip (GET after PUT)
        var fetched = await GetTest<ContentDetailsDto>(
            $"{itemsUrl}/{created.Id}",
            HttpStatusCode.OK);

        fetched.Should().NotBeNull();
        fetched!.Seo.Should().NotBeNull();
        fetched.Seo!.MetaTitle.Should().Be(seo.MetaTitle);
        fetched.Seo.MetaDescription.Should().Be(seo.MetaDescription);
        fetched.Seo.CanonicalUrl.Should().Be(seo.CanonicalUrl);
        fetched.Seo.OpenGraphTitle.Should().Be(seo.OpenGraphTitle);
        fetched.Seo.OpenGraphDescription.Should().Be(seo.OpenGraphDescription);
        fetched.Seo.OpenGraphImageUrl.Should().Be(seo.OpenGraphImageUrl);
        fetched.Seo.Robots.Should().Be(seo.Robots);
        fetched.Seo.Keywords.Should().Equal(seo.Keywords);
    }

    [Fact]
    public async Task PutContent_WithExistingSeo_ShouldUpdateAllSeoFields()
    {
        var typeUid = $"seo-update-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, supportsPreviewSlug: false, supportsSeo: true);

        var initialSeo = new SeoMetadataDto
        {
            MetaTitle = "Initial title",
            MetaDescription = "Initial description",
        };

        var created = await PostTest<ContentDetailsDto>(
            itemsUrl,
            new TestContent(Guid.NewGuid().ToString("N"))
            {
                Type = typeUid,
                Seo = initialSeo,
            },
            HttpStatusCode.Created);

        created.Should().NotBeNull();
        created!.Seo.Should().NotBeNull();

        var updatedSeo = new SeoMetadataDto
        {
            MetaTitle = "Updated SEO title",
            MetaDescription = "Updated SEO description",
            CanonicalUrl = "https://example.com/updated",
            OpenGraphTitle = "Updated OG Title",
            OpenGraphDescription = "Updated OG Description",
            OpenGraphImageUrl = "/api/images/updated-og.png",
            Robots = "index,follow",
            Keywords = new[] { "updated", "seo" },
        };

        var putDto = new ContentCreateDto
        {
            Title = created.Title,
            Description = created.Description,
            Body = created.Body,
            CoverImageUrl = created.CoverImageUrl,
            CoverImageAlt = created.CoverImageAlt,
            Slug = created.Slug,
            PreviewSlug = created.PreviewSlug,
            Seo = updatedSeo,
            Type = created.Type,
            Author = created.Author,
            Language = created.Language,
            TranslationKey = created.TranslationKey,
            Category = created.Category,
            Tags = created.Tags,
            AllowComments = created.AllowComments,
            Source = created.Source,
            PublishedAt = created.PublishedAt,
        };

        var putResponse = await Request(HttpMethod.Put, $"{itemsUrl}/{created.Id}", putDto);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify via GET that all SEO fields survived the database round-trip
        var fetched = await GetTest<ContentDetailsDto>(
            $"{itemsUrl}/{created.Id}",
            HttpStatusCode.OK);

        fetched.Should().NotBeNull();
        fetched!.Seo.Should().NotBeNull();
        fetched.Seo!.MetaTitle.Should().Be(updatedSeo.MetaTitle);
        fetched.Seo.MetaDescription.Should().Be(updatedSeo.MetaDescription);
        fetched.Seo.CanonicalUrl.Should().Be(updatedSeo.CanonicalUrl);
        fetched.Seo.OpenGraphTitle.Should().Be(updatedSeo.OpenGraphTitle);
        fetched.Seo.OpenGraphDescription.Should().Be(updatedSeo.OpenGraphDescription);
        fetched.Seo.OpenGraphImageUrl.Should().Be(updatedSeo.OpenGraphImageUrl);
        fetched.Seo.Robots.Should().Be(updatedSeo.Robots);
        fetched.Seo.Keywords.Should().Equal(updatedSeo.Keywords);
    }

    [Fact]
    public async Task ContentType_ShouldPersistSupportsPreviewSlug()
    {
        var uid = $"preview-type-{Guid.NewGuid():N}";

        var contentType = await EnsureContentTypeAsync(uid, supportsPreviewSlug: true);

        contentType.Should().NotBeNull();
        contentType!.SupportsPreviewSlug.Should().BeTrue();

        var fetched = await GetTest<ContentTypeDetailsDto>($"{ContentTypesApi}/{contentType.Id}", HttpStatusCode.OK);
        fetched.Should().NotBeNull();
        fetched!.SupportsPreviewSlug.Should().BeTrue();
    }

    [Fact]
    public async Task CreateContent_WithPreviewSlug_ShouldPersistRegardlessOfTypeSetting()
    {
        var typeUid = $"preview-disabled-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, supportsPreviewSlug: false);

        var content = new TestContent(Guid.NewGuid().ToString("N"))
        {
            Type = typeUid,
            PreviewSlug = $"preview-{Guid.NewGuid():N}",
        };

        var created = await PostTest<ContentDetailsDto>(itemsUrl, content, HttpStatusCode.Created);

        created.Should().NotBeNull();
        created!.PreviewSlug.Should().Be(content.PreviewSlug);
    }

    [Fact]
    public async Task ContentType_ShouldPersistSlugPrefixAndPostfix()
    {
        var uid = $"slug-affix-type-{Guid.NewGuid():N}";

        var contentType = await EnsureContentTypeAsync(uid, slugPrefix: "blog/", slugPostfix: ".html");

        contentType.Should().NotBeNull();
        contentType!.SlugPrefix.Should().Be("blog/");
        contentType!.SlugPostfix.Should().Be(".html");

        var fetched = await GetTest<ContentTypeDetailsDto>($"{ContentTypesApi}/{contentType.Id}", HttpStatusCode.OK);
        fetched.Should().NotBeNull();
        fetched!.SlugPrefix.Should().Be("blog/");
        fetched!.SlugPostfix.Should().Be(".html");
    }

    [Fact]
    public async Task CreateContent_WithMatchingSlugPrefix_ShouldSucceed()
    {
        var typeUid = $"prefix-type-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, slugPrefix: "blog/");

        var content = new TestContent(Guid.NewGuid().ToString("N"))
        {
            Type = typeUid,
            Slug = $"blog/my-post-{Guid.NewGuid():N}",
        };

        var created = await PostTest<ContentDetailsDto>(itemsUrl, content, HttpStatusCode.Created);
        created.Should().NotBeNull();
        created!.Slug.Should().StartWith("blog/");
    }

    [Fact]
    public async Task CreateContent_WithMismatchedSlugPrefix_ShouldReturn422()
    {
        var typeUid = $"prefix-fail-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, slugPrefix: "blog/");

        var content = new TestContent(Guid.NewGuid().ToString("N"))
        {
            Type = typeUid,
            Slug = $"news/my-post-{Guid.NewGuid():N}",
        };

        var response = await Request(HttpMethod.Post, itemsUrl, content);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(responseContent);
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Contain("blog/");
    }

    [Fact]
    public async Task CreateContent_WithMismatchedSlugPostfix_ShouldReturn422()
    {
        var typeUid = $"postfix-fail-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, slugPostfix: ".html");

        var content = new TestContent(Guid.NewGuid().ToString("N"))
        {
            Type = typeUid,
            Slug = $"my-post-{Guid.NewGuid():N}",
        };

        var response = await Request(HttpMethod.Post, itemsUrl, content);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(responseContent);
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Contain(".html");
    }

    [Fact]
    public async Task PatchContent_WithMismatchedSlugPrefix_ShouldReturn422()
    {
        var typeUid = $"prefix-patch-{Guid.NewGuid():N}";
        await EnsureContentTypeAsync(typeUid, slugPrefix: "blog/");

        var content = new TestContent(Guid.NewGuid().ToString("N"))
        {
            Type = typeUid,
            Slug = $"blog/my-post-{Guid.NewGuid():N}",
        };

        var created = await PostTest<ContentDetailsDto>(itemsUrl, content, HttpStatusCode.Created);
        created.Should().NotBeNull();

        var response = await Patch($"{itemsUrl}/{created!.Id}", new ContentUpdateDto { Slug = "news/bad-slug" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    protected override ContentUpdateDto UpdateItem(TestContent to)
    {
        var from = new ContentUpdateDto();
        to.Author = from.Author = to.Author + " Updated";
        return from;
    }

    private async Task<ContentTypeDetailsDto?> EnsureContentTypeAsync(
        string uid,
        bool supportsPreviewSlug = false,
        bool supportsSeo = false,
        string? slugPrefix = null,
        string? slugPostfix = null)
    {
        var existing = await GetTest<List<ContentTypeDetailsDto>>($"{ContentTypesApi}?filter[where][uid][eq]={uid}", HttpStatusCode.OK);
        if (existing != null && existing.Count > 0)
        {
            return existing[0];
        }

        return await PostTest<ContentTypeDetailsDto>(
            ContentTypesApi,
            new ContentTypeCreateDto
            {
                Uid = uid,
                Format = ContentFormat.MD,
                SupportsComments = true,
                SupportsCoverImage = true,
                SupportsPreviewSlug = supportsPreviewSlug,
                SupportsSEO = supportsSeo,
                SlugPrefix = slugPrefix,
                SlugPostfix = slugPostfix,
            },
            HttpStatusCode.Created);
    }
}