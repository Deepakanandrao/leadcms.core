// <copyright file="ContentTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Tests;

public class ContentTests : SimpleTableTests<Content, TestContent, ContentUpdateDto, IEntityService<Content>>
{
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
        updatedItem.Tags.Should().BeEmpty();
    }

    protected override ContentUpdateDto UpdateItem(TestContent to)
    {
        var from = new ContentUpdateDto();
        to.Author = from.Author = to.Author + " Updated";
        return from;
    }
}