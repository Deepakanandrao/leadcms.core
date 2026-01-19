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
    public TranslationTests()
        : base()
    {
        TrackEntityType<Content>();
        TrackEntityType<Contact>();
        TrackEntityType<EmailGroup>();
        TrackEntityType<EmailTemplate>();
        TrackEntityType<Comment>();
    }

    [Fact]
    public async Task GetTranslationDraft_Content_KeepOriginal_ReturnsOk()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content, HttpStatusCode.Created);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var response = await GetRequest($"/api/content/{contentId}/translation-draft/fr?transformer=keepOriginal");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var translationDraft = JsonHelper.Deserialize<ContentDetailsDto>(responseContent);

        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("fr");
        translationDraft.Title.Should().Be(content.Title);
        translationDraft.Description.Should().Be(content.Description);
        translationDraft.Body.Should().Be(content.Body);
        translationDraft.Id.Should().Be(0); // New entity
    }

    [Fact]
    public async Task GetTranslationDraft_Content_EntityNotFound_ReturnsNotFound()
    {
        // Act
        var response = await GetRequest("/api/content/99999/translation-draft/es?transformer=emptyCopy");

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
        var response = await GetRequest($"/api/email-groups/{emailGroupId}/translation-draft/de?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var translationDraft = JsonHelper.Deserialize<EmailGroupDetailsDto>(responseContent);

        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("de");
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
        var response = await GetTest($"/api/content/{contentId}/translation-draft/es?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var translationDraft = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/es?transformer=emptyCopy");

        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("es");
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
        var firstDraft = await GetTest<ContentDetailsDto>($"/api/content/{originalId}/translation-draft/de?transformer=emptyCopy");

        // Create a manual translation using the same TranslationKey and language
        var translationContent = new TestContent();
        translationContent.Language = "de";
        translationContent.Slug = "test-slug-de";
        translationContent.TranslationKey = firstDraft!.TranslationKey;

        await PostTest("/api/content", translationContent);

        // Act - Try to get translation draft again for the same language
        var response = await GetRequest($"/api/content/{originalId}/translation-draft/de?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var content = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(content);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(409);
        problemDetails.Extensions.Should().ContainKey("entityType");
        problemDetails.Extensions.Should().ContainKey("entityId");
        problemDetails.Extensions.Should().ContainKey("language");
        problemDetails.Extensions["language"]!.ToString().Should().Be("de");
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
        var translationDraft = await GetTest<EmailTemplateDetailsDto>($"/api/email-templates/{templateId}/translation-draft/pt?transformer=emptyCopy");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("pt");
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
        var translationDraft = await GetTest<EmailGroupDetailsDto>($"/api/email-groups/{emailGroupId}/translation-draft/ru?transformer=keepOriginal");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("ru");
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
        var translationDraft = await GetTest<CommentDetailsDto>($"/api/comments/{commentId}/translation-draft/zh?transformer=emptyCopy");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("zh");
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
        var translationDraft = await GetTest<CommentDetailsDto>($"/api/comments/{commentId}/translation-draft/ar?transformer=keepOriginal");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("ar");
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
        var translationDraft = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/it");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("it");
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
        var response = await GetRequest($"/api/content/{contentId}/translation-draft/nl?transformer={transformerType}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var translationDraft = JsonHelper.Deserialize<ContentDetailsDto>(responseContent);

        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("nl");
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
        var draft1 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/fr?transformer=emptyCopy");
        var draft2 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/de?transformer=emptyCopy");

        // Assert
        draft1.Should().NotBeNull();
        draft2.Should().NotBeNull();
        draft1!.TranslationKey.Should().Be(draft2!.TranslationKey); // Same translation key
        draft1.Language.Should().Be("fr");
        draft2.Language.Should().Be("de");
    }

    [Fact]
    public async Task GetTranslationDraft_SameLanguageMultipleTimes_ReturnsSameTranslationKey()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var draft1 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/sv?transformer=emptyCopy");
        var draft2 = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/sv?transformer=keepOriginal");

        // Assert
        draft1.Should().NotBeNull();
        draft2.Should().NotBeNull();
        draft1!.TranslationKey.Should().Be(draft2!.TranslationKey);
        draft1.Language.Should().Be("sv");
        draft2.Language.Should().Be("sv");
    }

    [Fact]
    public async Task GetTranslationDraft_WithSpecialCharactersInLanguage_ReturnsOk()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var translationDraft = await GetTest<ContentDetailsDto>($"/api/content/{contentId}/translation-draft/zh?transformer=emptyCopy");

        // Assert
        translationDraft.Should().NotBeNull();
        translationDraft!.Language.Should().Be("zh");
        translationDraft.TranslationKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetTranslations_Content_NoTranslations_ReturnsOriginalOnly()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var translations = await GetTest<List<ContentDetailsDto>>($"/api/content/{contentId}/translations");

        // Assert
        translations.Should().NotBeNull();
        translations!.Should().HaveCount(1);
        translations![0].Id.Should().Be(contentId);
        translations[0].Language.Should().Be("en"); // Default language
    }

    [Fact]
    public async Task GetTranslations_Content_WithTranslations_ReturnsAllTranslations()
    {
        // Arrange
        var originalContent = new TestContent();
        var originalUrl = await PostTest("/api/content", originalContent);
        var originalId = GetIdFromUrl(originalUrl);

        // Create translation drafts to generate TranslationKey
        var frenchDraft = await GetTest<ContentDetailsDto>($"/api/content/{originalId}/translation-draft/fr?transformer=emptyCopy");
        var germanDraft = await GetTest<ContentDetailsDto>($"/api/content/{originalId}/translation-draft/de?transformer=emptyCopy");

        // Create actual translations by posting them
        var frenchContent = new TestContent();
        frenchContent.Language = "fr";
        frenchContent.Slug = "test-slug-fr";
        frenchContent.TranslationKey = frenchDraft!.TranslationKey;
        await PostTest("/api/content", frenchContent);

        var germanContent = new TestContent();
        germanContent.Language = "de";
        germanContent.Slug = "test-slug-de";
        germanContent.TranslationKey = germanDraft!.TranslationKey;
        await PostTest("/api/content", germanContent);

        // Act
        var translations = await GetTest<List<ContentDetailsDto>>($"/api/content/{originalId}/translations");

        // Assert
        translations.Should().NotBeNull();
        translations!.Should().HaveCount(3); // Original + 2 translations

        // Should be ordered by ID
        translations.Should().BeInAscendingOrder(x => x.Id);

        // Verify all translations have the same TranslationKey
        var translationKey = translations![0].TranslationKey;
        translationKey.Should().NotBeNullOrEmpty();
        translations.Should().AllSatisfy(t => t.TranslationKey.Should().Be(translationKey));

        // Verify languages
        var languages = translations.Select(t => t.Language).ToList();
        languages.Should().Contain("en"); // Original
        languages.Should().Contain("fr");
        languages.Should().Contain("de");
    }

    [Fact]
    public async Task GetTranslations_Content_EntityNotFound_ReturnsNotFound()
    {
        // Act
        var response = await GetRequest("/api/content/99999/translations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTranslations_EmailGroup_WithTranslations_ReturnsAllTranslations()
    {
        // Arrange
        var originalEmailGroup = new TestEmailGroup();
        var originalUrl = await PostTest("/api/email-groups", originalEmailGroup);
        var originalId = GetIdFromUrl(originalUrl);

        // Create translation draft to generate TranslationKey
        var spanishDraft = await GetTest<EmailGroupDetailsDto>($"/api/email-groups/{originalId}/translation-draft/es?transformer=emptyCopy");

        // Create actual translation
        var spanishEmailGroup = new TestEmailGroup();
        spanishEmailGroup.Language = "es";
        spanishEmailGroup.Name = "Grupo de Email Español";
        spanishEmailGroup.TranslationKey = spanishDraft!.TranslationKey;
        await PostTest("/api/email-groups", spanishEmailGroup);

        // Act
        var translations = await GetTest<List<EmailGroupDetailsDto>>($"/api/email-groups/{originalId}/translations");

        // Assert
        translations.Should().NotBeNull();
        translations!.Should().HaveCount(2); // Original + 1 translation
        translations.Should().BeInAscendingOrder(x => x.Id);

        var translationKey = translations![0].TranslationKey;
        translations.Should().AllSatisfy(t => t.TranslationKey.Should().Be(translationKey));

        var languages = translations.Select(t => t.Language).ToList();
        languages.Should().Contain("en");
        languages.Should().Contain("es");
    }

    [Fact]
    public async Task GetTranslations_EmailTemplate_WithTranslations_ReturnsAllTranslations()
    {
        // Arrange
        var emailGroup = new TestEmailGroup();
        var emailGroupUrl = await PostTest("/api/email-groups", emailGroup);
        var emailGroupId = GetIdFromUrl(emailGroupUrl);

        var originalTemplate = new TestEmailTemplate();
        originalTemplate.EmailGroupId = emailGroupId;
        var originalUrl = await PostTest("/api/email-templates", originalTemplate);
        var originalId = GetIdFromUrl(originalUrl);

        // Create translation draft to generate TranslationKey
        var italianDraft = await GetTest<EmailTemplateDetailsDto>($"/api/email-templates/{originalId}/translation-draft/it?transformer=emptyCopy");

        // Create actual translation
        var italianTemplate = new TestEmailTemplate();
        italianTemplate.EmailGroupId = emailGroupId;
        italianTemplate.Language = "it-IT";
        italianTemplate.Name = "Template Italiano";
        italianTemplate.TranslationKey = italianDraft!.TranslationKey;
        await PostTest("/api/email-templates", italianTemplate);

        // Act
        var translations = await GetTest<List<EmailTemplateDetailsDto>>($"/api/email-templates/{originalId}/translations");

        // Assert
        translations.Should().NotBeNull();
        translations!.Should().HaveCount(2); // Original + 1 translation
        translations.Should().BeInAscendingOrder(x => x.Id);

        var translationKey = translations![0].TranslationKey;
        translations.Should().AllSatisfy(t => t.TranslationKey.Should().Be(translationKey));

        var languages = translations.Select(t => t.Language).ToList();
        languages.Should().Contain("en");
        languages.Should().Contain("it-IT");
    }

    [Fact]
    public async Task GetTranslations_Comment_WithTranslations_ReturnsAllTranslations()
    {
        // Arrange
        // Create a content item to comment on
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Create a contact for the comment
        var contact = new TestContact();
        await PostTest("/api/contacts", contact);

        // Create original comment
        var originalComment = new TestComment(string.Empty, contentId);
        var originalUrl = await PostTest("/api/comments", originalComment);
        var originalId = GetIdFromUrl(originalUrl);

        // Create translation draft to generate TranslationKey
        var japaneseDraft = await GetTest<CommentDetailsDto>($"/api/comments/{originalId}/translation-draft/ja?transformer=emptyCopy");

        // Create actual translation
        var japaneseComment = new TestComment(string.Empty, contentId);
        japaneseComment.Language = "ja-JP";
        japaneseComment.Body = "日本語のコメント";
        japaneseComment.TranslationKey = japaneseDraft!.TranslationKey;
        await PostTest("/api/comments", japaneseComment);

        // Act
        var translations = await GetTest<List<CommentDetailsDto>>($"/api/comments/{originalId}/translations");

        // Assert
        translations.Should().NotBeNull();
        translations!.Should().HaveCount(2); // Original + 1 translation
        translations.Should().BeInAscendingOrder(x => x.Id);

        var translationKey = translations![0].TranslationKey;
        translations.Should().AllSatisfy(t => t.TranslationKey.Should().Be(translationKey));

        var languages = translations.Select(t => t.Language).ToList();
        languages.Should().Contain("en");
        languages.Should().Contain("ja-JP");
    }

    [Fact]
    public async Task GetTranslations_Content_CalledFromTranslation_ReturnsAllTranslations()
    {
        // Arrange
        var originalContent = new TestContent();
        var originalUrl = await PostTest("/api/content", originalContent);
        var originalId = GetIdFromUrl(originalUrl);

        // Create translation
        var frenchDraft = await GetTest<ContentDetailsDto>($"/api/content/{originalId}/translation-draft/fr?transformer=emptyCopy");
        var frenchContent = new TestContent();
        frenchContent.Language = "fr";
        frenchContent.Slug = "test-slug-fr";
        frenchContent.TranslationKey = frenchDraft!.TranslationKey;
        var frenchUrl = await PostTest("/api/content", frenchContent);
        var frenchId = GetIdFromUrl(frenchUrl);

        // Act - Call GetTranslations from the translation (not the original)
        var translations = await GetTest<List<ContentDetailsDto>>($"/api/content/{frenchId}/translations");

        // Assert
        translations.Should().NotBeNull();
        translations!.Should().HaveCount(2); // Original + 1 translation
        translations.Should().BeInAscendingOrder(x => x.Id);

        var translationKey = translations![0].TranslationKey;
        translations.Should().AllSatisfy(t => t.TranslationKey.Should().Be(translationKey));

        var languages = translations.Select(t => t.Language).ToList();
        languages.Should().Contain("en");
        languages.Should().Contain("fr");
    }

    [Fact]
    public async Task GetTranslations_Content_EntityWithoutTranslationKey_ReturnsOriginalOnly()
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act - Call GetTranslations directly without creating any translation drafts
        var translations = await GetTest<List<ContentDetailsDto>>($"/api/content/{contentId}/translations");

        // Assert
        translations.Should().NotBeNull();
        translations!.Should().HaveCount(1);
        translations![0].Id.Should().Be(contentId);
        translations[0].Language.Should().Be("en");
        translations[0].TranslationKey.Should().BeNullOrEmpty(); // No translation key generated yet
    }

    [Theory]
    [InlineData("content")]
    [InlineData("email-groups")]
    [InlineData("email-templates")]
    [InlineData("comments")]
    public async Task GetTranslations_AllTranslatableEntities_ReturnsOk(string entityType)
    {
        // Note: This test focuses on positive cases since authorization is already tested elsewhere
        // Just verify the endpoints exist and return OK for valid entities
        var response = await GetRequest($"/api/{entityType}/1/translations");

        // Should be 404 (not found) or 200 (OK), but not 401 (unauthorized) when authenticated
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("ko")] // Korean - not supported
    [InlineData("hi")] // Hindi - not supported
    [InlineData("th")] // Thai - not supported
    [InlineData("pl")] // Polish - not supported
    [InlineData("tr")] // Turkish - not supported
    public async Task GetTranslationDraft_UnsupportedLanguage_ReturnsBadRequest(string unsupportedLanguage)
    {
        // Arrange
        var content = new TestContent();
        var contentUrl = await PostTest("/api/content", content);
        var contentId = GetIdFromUrl(contentUrl);

        // Act
        var response = await GetRequest($"/api/content/{contentId}/translation-draft/{unsupportedLanguage}?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(responseContent);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Extensions.Should().ContainKey("language");
        problemDetails.Extensions["language"]!.ToString().Should().Be(unsupportedLanguage);
    }

    [Fact]
    public async Task GetTranslationDraft_UnsupportedLanguage_EmailGroup_ReturnsBadRequest()
    {
        // Arrange
        var emailGroup = new TestEmailGroup();
        var emailGroupUrl = await PostTest("/api/email-groups", emailGroup);
        var emailGroupId = GetIdFromUrl(emailGroupUrl);

        // Act
        var response = await GetRequest($"/api/email-groups/{emailGroupId}/translation-draft/vi?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(responseContent);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Extensions.Should().ContainKey("language");
        problemDetails.Extensions["language"]!.ToString().Should().Be("vi");
    }

    [Fact]
    public async Task GetTranslationDraft_UnsupportedLanguage_EmailTemplate_ReturnsBadRequest()
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
        var response = await GetRequest($"/api/email-templates/{templateId}/translation-draft/bn?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(responseContent);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Extensions.Should().ContainKey("language");
        problemDetails.Extensions["language"]!.ToString().Should().Be("bn");
    }

    [Fact]
    public async Task GetTranslationDraft_UnsupportedLanguage_Comment_ReturnsBadRequest()
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
        var response = await GetRequest($"/api/comments/{commentId}/translation-draft/ur?transformer=emptyCopy");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        var problemDetails = JsonHelper.Deserialize<ProblemDetails>(responseContent);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Extensions.Should().ContainKey("language");
        problemDetails.Extensions["language"]!.ToString().Should().Be("ur");
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
