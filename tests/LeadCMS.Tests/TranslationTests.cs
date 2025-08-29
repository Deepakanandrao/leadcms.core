// <copyright file="TranslationTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LeadCMS.Tests;

/// <summary>
/// Integration tests for translation functionality across all translatable entities.
/// </summary>
public class TranslationTests : BaseTestAutoLogin
{
    [Fact]
    public async Task GetTranslationDraft_Content_KeepOriginal_ReturnsOk()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content, HttpStatusCode.Created);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var response = await GetRequest($"/api/content/{contentId}/translation-draft/fr-FR?transformer=keepOriginal");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var translationDraft = JsonHelper.Deserialize<ContentDetailsDto>(responseContent);
        
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("fr-FR");
        translationDraft.Title.Should().Be(content.Title);
        translationDraft.Description.Should().Be(content.Description);
        translationDraft.Body.Should().Be(content.Body);
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_Content_EntityNotFound_ReturnsNotFound()
    {
        // Act
        var response = await GetRequest("/api/content/99999/translation-draft/es-ES?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTranslationDraft_EmailGroup_EmptyCopy_ReturnsOk()
    {
        // Arrange
        var emailGroup = new TestEmailGroup();
        var emailGroupUrl = await PostTest("/api/email-groups", emailGroup, HttpStatusCode.Created);
        var emailGroupId = GetIdFromUrl(emailGroupUrl);

        // Act
        var response = await GetRequest($"/api/email-groups/{emailGroupId}/translation-draft/de-DE?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var translationDraft = JsonHelper.Deserialize<EmailGroupDetailsDto>(responseContent);
        
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("de-DE");
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_Content_EmptyCopy_ReturnsOk()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var response = await GetTest($"/api/content/{contentId}/translation-draft/es-ES?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var translationDraft = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/es-ES?transformer=emptyCopy");
        
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("es-ES");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
        translationDraft.Title.Should().BeEmpty();
        translationDraft.Description.Should().BeEmpty();
        translationDraft.Body.Should().BeEmpty();
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_Content_TranslationAlreadyExists_ReturnsConflict()
    {
        // Arrange
        var originalContent = new TestContent();
        var originalUrl = await PostTest("/api/content", originalContent);
        var originalId = GetIdFromUrl(originalUrl);

        // First, call the translation draft endpoint to generate a TranslationKey for the original content
        var firstDraft = await GetTest<ContentDetailsDto>($"/api/content/{originalId}/translation-draft/de-DE?transformer=emptyCopy");
        
        // Create a manual translation using the same TranslationKey and language
        var translationContent = new TestContent();
        translationContent.Language = "de-DE";
        translationContent.Slug = "test-slug-de";
        translationContent.TranslationKey = firstDraft!.TranslationKey;
        
        await PostTest("/api/content", translationContent);

        // Act - Try to get translation draft again for the same language
        var response = await GetRequest($"/api/content/{originalId}/translation-draft/de-DE?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(content);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(409);
        problemDetails.Extensions.Should().ContainKey("entityType");
        problemDetails.Extensions.Should().ContainKey("entityId");
        problemDetails.Extensions.Should().ContainKey("language");
        problemDetails.Extensions["language"]!.ToString().Should().Be("de-DE");
    }

    [Fact]
    public async Task GetTranslationDraft_EmailTemplate_EmptyCopy_ReturnsOk()
    {
        // Arrange
        var emailGroup = new TestEmailGroup();
        var emailGroupUrl = await PostTest("/api/email-groups", emailGroup);
        var emailGroupId = GetIdFromUrl(emailGroupUrl);

        var emailTemplate = new TestEmailTemplate();
        emailTemplate.EmailGroupId = emailGroupId;
        var templateUrl = await PostTest("/api/email-templates", emailTemplate);
        var templateId = GetIdFromUrl(templateUrl);

        // Act
        var translationDraft = await GetTest<EmailTemplateDetailsDto>($"/api/email-templates/{templateId}/translation-draft/pt-BR?transformer=emptyCopy");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("pt-BR");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
        translationDraft.Name.Should().BeEmpty();
        translationDraft.Subject.Should().BeEmpty();
        translationDraft.BodyTemplate.Should().BeEmpty();
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_EmailGroup_KeepOriginal_ReturnsOk()
    {
        // Arrange
        var emailGroup = new TestEmailGroup();
        var emailGroupUrl = await PostTest("/api/email-groups", emailGroup);
        var emailGroupId = GetIdFromUrl(emailGroupUrl);

        // Act
        var translationDraft = await GetTest<EmailGroupDetailsDto>($"/api/email-groups/{emailGroupId}/translation-draft/ru-RU?transformer=keepOriginal");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("ru-RU");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
        translationDraft.Name.Should().Be(emailGroup.Name);
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_Comment_EmptyCopy_ReturnsOk()
    {
        // Arrange
        // Create a content item to comment on
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Create a contact for the comment
        var contact = new TestContact();
        await PostTest("/api/contacts", contact);

        // Create a comment
        var comment = new TestComment(string.Empty, contentId);
        var commentUrl = await PostTest("/api/comments", comment);
        var commentId = GetIdFromUrl(commentUrl);

        // Act
        var translationDraft = await GetTest<CommentDetailsDto>($"/api/comments/{commentId}/translation-draft/zh-CN?transformer=emptyCopy");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("zh-CN");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
        translationDraft.Body.Should().BeEmpty();
        translationDraft.AuthorName.Should().BeEmpty();
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_Comment_KeepOriginal_ReturnsOk()
    {
        // Arrange
        // Create a content item to comment on
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Create a contact for the comment
        var contact = new TestContact();
        await PostTest("/api/contacts", contact);

        // Create a comment
        var comment = new TestComment(string.Empty, contentId);
        var commentUrl = await PostTest("/api/comments", comment);
        var commentId = GetIdFromUrl(commentUrl);

        // Act
        var translationDraft = await GetTest<CommentDetailsDto>($"/api/comments/{commentId}/translation-draft/ar-SA?transformer=keepOriginal");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("ar-SA");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
        translationDraft.Body.Should().Be(comment.Body);
        translationDraft.AuthorName.Should().Be(comment.AuthorName);
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_InvalidTransformerType_UsesDefaultEmptyCopy()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act - using invalid transformer type should default to emptyCopy
        var translationDraft = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/it-IT");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("it-IT");
        translationDraft.Title.Should().BeEmpty(); // Empty copy behavior
        translationDraft.Description.Should().BeEmpty();
        translationDraft.Body.Should().BeEmpty();
    }

    [Theory]
    [InlineData("emptyCopy")]
    [InlineData("keepOriginal")]
    [InlineData("EMPTYCOPY")] // Test case insensitivity
    [InlineData("KEEPORIGINAL")]
    public async Task GetTranslationDraft_AllTransformerTypes_ReturnsOk(string transformerType)
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var response = await GetRequest($"/api/content/{contentId}/translation-draft/nl-NL?transformer={transformerType}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var translationDraft = JsonHelper.Deserialize<ContentDetailsDto>(responseContent);
        
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("nl-NL");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
        
        // Verify behavior based on transformer type
        if (transformerType.ToLower() == "emptycopy")
        {
            translationDraft.Title.Should().BeEmpty();
            translationDraft.Description.Should().BeEmpty();
            translationDraft.Body.Should().BeEmpty();
        }
        else if (transformerType.ToLower() == "keeporiginal")
        {
            translationDraft.Title.Should().Be(content.Title);
            translationDraft.Description.Should().Be(content.Description);
            translationDraft.Body.Should().Be(content.Body);
        }
    }

    [Fact]
    public async Task GetTranslationDraft_MultipleLanguages_GeneratesSameTranslationKeys()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var draft1 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/fr-FR?transformer=emptyCopy");
        var draft2 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/de-DE?transformer=emptyCopy");

        // Assert
        draft1.Should().NotBeNull();
        draft2.Should().NotBeNull();
        draft1!.TranslationKey.Should().Be(draft2!.TranslationKey); // Same translation key
        draft1.Language.Should().Be("fr-FR");
        draft2.Language.Should().Be("de-DE");
    }

    [Fact]
    public async Task GetTranslationDraft_SameLanguageMultipleTimes_ReturnsSameTranslationKey()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var draft1 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/sv-SE?transformer=emptyCopy");
        var draft2 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/sv-SE?transformer=keepOriginal");

        // Assert
        draft1.Should().NotBeNull();
        draft2.Should().NotBeNull();
        draft1!.TranslationKey.Should().Be(draft2!.TranslationKey);
        draft1.Language.Should().Be("sv-SE");
        draft2.Language.Should().Be("sv-SE");
    }

    [Fact]
    public async Task GetTranslationDraft_WithSpecialCharactersInLanguage_ReturnsOk()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var translationDraft = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/zh-Hans-CN?transformer=emptyCopy");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("zh-Hans-CN");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Helper method to extract ID from a URL like "/api/content/123".
    /// </summary>
    private static int GetIdFromUrl(string url)
    {
        var parts = url.Split('/');
        return int.Parse(parts[^1]);
    }
}
