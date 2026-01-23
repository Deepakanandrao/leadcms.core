// <copyright file="ContentGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using AutoMapper;
using LeadCMS.Constants;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Plugin.AI.Configuration;
using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace LeadCMS.Plugin.AI.Services;

public class ContentGenerationService : IContentGenerationService
{
    private readonly PgDbContext dbContext;
    private readonly ITextGenerationService textGenerationService;
    private readonly IMdxComponentParserService mdxComponentParserService;
    private readonly IMapper mapper;
    private readonly ISettingService settingService;
    private readonly UserManager<User> userManager;
    private readonly IHttpContextAccessor httpContextAccessor;

    public ContentGenerationService(
        PgDbContext dbContext,
        ITextGenerationService textGenerationService,
        IMdxComponentParserService mdxComponentParserService,
        IMapper mapper,
        ISettingService settingService,
        UserManager<User> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        this.dbContext = dbContext;
        this.textGenerationService = textGenerationService;
        this.mdxComponentParserService = mdxComponentParserService;
        this.mapper = mapper;
        this.settingService = settingService;
        this.userManager = userManager;
        this.httpContextAccessor = httpContextAccessor;
    }

    public async Task<ContentDetailsDto> GenerateContentAsync(ContentGenerationRequest request)
    {
        Log.Information("Starting content generation for type {ContentType} in language {Language}", request.ContentType, request.Language);

        // Step 1: Validate content type exists
        var contentType = await dbContext.ContentTypes!
            .FirstOrDefaultAsync(ct => ct.Uid == request.ContentType);

        if (contentType == null)
        {
            throw new AIContentTypeNotFoundException(request.ContentType);
        }

        // Step 2: Find sample content records
        var sampleContent = await FindSampleContentAsync(request.ContentType, request.Language, request.ReferenceContentId);

        if (sampleContent == null)
        {
            throw new AIProviderException(
                "ContentGeneration",
                "Not enough data in the database for the AI assistant to generate new content records. Please create at least one content record of this type first.");
        }

        // Step 3: Get MDX component information if it's an MDX content type
        MdxComponentAnalysisDto? componentAnalysis = null;
        if (contentType.Format == ContentFormat.MDX || contentType.Format == ContentFormat.MD)
        {
            try
            {
                componentAnalysis = await mdxComponentParserService.AnalyzeContentTypeAsync(request.ContentType);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to analyze MDX components for content type {ContentType}, proceeding without component information", request.ContentType);
            }
        }

        // Step 4: Build prompts and generate content
        var systemPrompt = await BuildSystemPromptAsync(contentType, sampleContent, componentAnalysis);
        var userPrompt = BuildUserPrompt(request.Prompt, request.Language);

        var textRequest = new TextGenerationRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
        };

        try
        {
            var response = await textGenerationService.GenerateTextAsync(textRequest);

            // Parse the generated JSON content
            var generatedContent = ParseGeneratedContent(response.GeneratedText);

            // Validate content length constraints
            var isValidLength = await ValidateContentLengthAsync(generatedContent.Title, generatedContent.Description);
            if (!isValidLength)
            {
                Log.Warning("Generated content does not meet length constraints, but proceeding with generation");
            }

            // Get current user's display name for AI-generated drafts
            var currentUser = await UserHelper.GetCurrentUserAsync(userManager, httpContextAccessor?.HttpContext?.User);
            var authorName = currentUser?.DisplayName ?? generatedContent.Author ?? sampleContent.Author;

            // Create a Content entity with the generated data
            var contentEntity = new Content
            {
                Title = generatedContent.Title,
                Description = generatedContent.Description,
                Body = generatedContent.Body,
                Slug = generatedContent.Slug,
                Author = authorName,
                Language = request.Language,
                Category = generatedContent.Category ?? sampleContent.Category,
                Tags = generatedContent.Tags ?? sampleContent.Tags,
                CoverImageAlt = generatedContent.CoverImageAlt ?? string.Empty,
                Type = request.ContentType,
                AllowComments = sampleContent.AllowComments,
                PublishedAt = DateTime.UtcNow,
                Source = $"AI Generated - Model: {response.Model}, Tokens: {response.TokensUsed}",
            };

            // Map to ContentDetailsDto
            var result = mapper.Map<ContentDetailsDto>(contentEntity);

            Log.Information("Successfully generated content for type {ContentType} in language {Language}", request.ContentType, request.Language);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate content for type {ContentType} in language {Language}", request.ContentType, request.Language);
            throw new AIProviderException("ContentGeneration", "Failed to generate content", ex);
        }
    }

    public async Task<ContentDetailsDto> GenerateContentEditAsync(ContentEditRequest request)
    {
        var systemPrompt = await BuildEditSystemPromptAsync();
        var userPrompt = BuildEditUserPrompt(request, request.Prompt);

        var textRequest = new TextGenerationRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
        };

        try
        {
            var response = await textGenerationService.GenerateTextAsync(textRequest);

            // Parse the generated JSON content
            var contentData = JsonSerializer.Deserialize<JsonElement>(response.GeneratedText);

            var title = contentData.TryGetProperty("title", out var titleProp) ? (titleProp.GetString() ?? request.Title ?? string.Empty) : (request.Title ?? string.Empty);
            var description = contentData.TryGetProperty("description", out var descProp) ? (descProp.GetString() ?? request.Description ?? string.Empty) : (request.Description ?? string.Empty);

            // Validate content length constraints
            var isValidLength = await ValidateContentLengthAsync(title, description);
            if (!isValidLength)
            {
                Log.Warning("Edited content does not meet length constraints, but proceeding with edit");
            }

            return new ContentDetailsDto
            {
                Title = title,
                Slug = contentData.TryGetProperty("slug", out var slugProp) ? (slugProp.GetString() ?? request.Slug ?? string.Empty) : (request.Slug ?? string.Empty),
                Description = description,
                Body = contentData.TryGetProperty("body", out var bodyProp) ? (bodyProp.GetString() ?? request.Body ?? string.Empty) : (request.Body ?? string.Empty),
                Tags = contentData.TryGetProperty("tags", out var tagsProp) ? GetStringArrayProperty(contentData, "tags") : (request.Tags ?? Array.Empty<string>()),
                Category = contentData.TryGetProperty("category", out var categoryProp) ? (categoryProp.GetString() ?? request.Category ?? string.Empty) : (request.Category ?? string.Empty),
                Author = request.Author ?? string.Empty,
                Type = request.Type ?? string.Empty,
                Language = request.Language ?? string.Empty,
                CoverImageUrl = request.CoverImageUrl,
                CoverImageAlt = request.CoverImageAlt,
                TranslationKey = request.TranslationKey,
                AllowComments = request.AllowComments ?? false,
                Source = request.Source,
                PublishedAt = request.PublishedAt,
            };
        }
        catch (JsonException ex)
        {
            throw new AIProviderException("ContentGeneration", $"Failed to parse AI response as JSON: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new AIProviderException("ContentGeneration", $"Failed to generate content edit: {ex.Message}", ex);
        }
    }

    private async Task<Content?> FindSampleContentAsync(string contentType, string language, int? referenceContentId)
    {
        if (referenceContentId.HasValue)
        {
            var referencedContent = await dbContext.Content!
                .FirstOrDefaultAsync(c => c.Id == referenceContentId.Value);

            if (referencedContent == null)
            {
                throw new AIProviderException(
                    "ContentGeneration",
                    $"Reference content with id {referenceContentId.Value} was not found.");
            }

            if (!string.Equals(referencedContent.Type, contentType, StringComparison.OrdinalIgnoreCase))
            {
                throw new AIProviderException(
                    "ContentGeneration",
                    $"Reference content id {referenceContentId.Value} does not match content type '{contentType}'.");
            }

            return referencedContent;
        }

        // First try to find content with the same language
        var sampleContent = await dbContext.Content!
            .Where(c => c.Type == contentType && c.Language == language)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        // If no content with the same language, try any language
        if (sampleContent == null)
        {
            sampleContent = await dbContext.Content!
                .Where(c => c.Type == contentType)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
        }

        return sampleContent;
    }

    private async Task<List<string>> AnalyzeSlugPatternsAsync(string contentType, int maxExamples = 5)
    {
        // Get existing slug patterns for the same content type
        var existingSlugs = await dbContext.Content!
            .Where(c => c.Type == contentType)
            .OrderByDescending(c => c.CreatedAt)
            .Take(maxExamples)
            .Select(c => c.Slug)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToListAsync();

        return existingSlugs;
    }

    private async Task<string> BuildSystemPromptAsync(ContentType contentType, Content sampleContent, MdxComponentAnalysisDto? componentAnalysis)
    {
        // Get content length constraints from settings/configuration
        var (minTitleLength, maxTitleLength, minDescriptionLength, maxDescriptionLength) = await GetContentLengthConstraintsAsync();

        // Analyze slug patterns from existing content of the same type
        var existingSlugPatterns = await AnalyzeSlugPatternsAsync(contentType.Uid);

        var siteProfileSection = await BuildSiteProfileSectionAsync();

        var prompt = $@"You are an expert content creator generating new content for a CMS. Generate a new {contentType.Uid} content record based on the sample and user requirements.

SAMPLE CONTENT STRUCTURE:
Title: {sampleContent.Title}
Description: {sampleContent.Description}
Author: {sampleContent.Author}
Category: {sampleContent.Category}
Tags: {JsonHelper.Serialize(sampleContent.Tags)}
Language: {sampleContent.Language}
Cover Image Alt: {sampleContent.CoverImageAlt}
Body Format: {contentType.Format}
Body Sample (first 500 chars): {(sampleContent.Body.Length > 500 ? sampleContent.Body.Substring(0, 500) + "..." : sampleContent.Body)}";

        if (!string.IsNullOrEmpty(siteProfileSection))
        {
            prompt += $@"

    SITE PROFILE:
    {siteProfileSection}";
        }

        if (componentAnalysis != null && componentAnalysis.Components.Any())
        {
            prompt += $@"

MDX COMPONENTS AVAILABLE:
This content type supports the following MDX components. You can use these in the body content:
{string.Join("\n", componentAnalysis.Components.Select(c => FormatComponentInfo(c)))}";
        }

        // Add slug pattern guidance if we have examples
        var slugPatternGuidance = string.Empty;
        if (existingSlugPatterns.Any())
        {
            slugPatternGuidance = $@"

SLUG PATTERN EXAMPLES:
Existing {contentType.Uid} content uses these slug patterns - follow the same convention:
{string.Join("\n", existingSlugPatterns.Select(slug => $"- {slug}"))}";
        }

        prompt += slugPatternGuidance;

        prompt += $@"

REQUIREMENTS:
1. Return ONLY valid JSON with the exact structure shown below
2. Generate original, high-quality content that matches the style and format of the sample
3. Ensure the body content is in {contentType.Format} format
4. Generate a slug that follows the same pattern as existing {contentType.Uid} content shown above (URL-friendly, lowercase, hyphen-separated)
5. Keep the same author, category structure, and tagging style as the sample
6. If using MDX format, you may include the available components listed above

CONTENT LENGTH REQUIREMENTS:
- Title: Must be between {minTitleLength} and {maxTitleLength} characters (SEO optimized)
- Description: Must be between {minDescriptionLength} and {maxDescriptionLength} characters (SEO optimized for meta descriptions)

REQUIRED JSON STRUCTURE:
{{
  ""title"": ""Generated title ({minTitleLength}-{maxTitleLength} characters)"",
  ""description"": ""Generated description ({minDescriptionLength}-{maxDescriptionLength} characters)"",
  ""body"": ""Generated body content in {contentType.Format} format"",
  ""slug"": ""url-friendly-slug"",
  ""author"": ""Author name"",
  ""category"": ""Category name"",
  ""tags"": [""tag1"", ""tag2""],
  ""coverImageAlt"": ""Alt text for cover image""
}}";

        return prompt;
    }

    private async Task<(int minTitleLength, int maxTitleLength, int minDescriptionLength, int maxDescriptionLength)> GetContentLengthConstraintsAsync()
    {
        // Use the new convention-based method calls
        var minTitleLength = await settingService.GetIntSettingWithFallbackAsync(
            SettingKeys.MinTitleLength,
            10); // Default minimum

        var maxTitleLength = await settingService.GetIntSettingWithFallbackAsync(
            SettingKeys.MaxTitleLength,
            60); // Default maximum

        var minDescriptionLength = await settingService.GetIntSettingWithFallbackAsync(
            SettingKeys.MinDescriptionLength,
            20); // Default minimum

        var maxDescriptionLength = await settingService.GetIntSettingWithFallbackAsync(
            SettingKeys.MaxDescriptionLength,
            155); // Default maximum

        return (minTitleLength, maxTitleLength, minDescriptionLength, maxDescriptionLength);
    }

    private async Task<bool> ValidateContentLengthAsync(string title, string description)
    {
        var (minTitleLength, maxTitleLength, minDescriptionLength, maxDescriptionLength) = await GetContentLengthConstraintsAsync();

        bool titleValid = title.Length >= minTitleLength && title.Length <= maxTitleLength;
        bool descriptionValid = description.Length >= minDescriptionLength && description.Length <= maxDescriptionLength;

        if (!titleValid)
        {
            Log.Warning(
                "Generated title length {TitleLength} is outside valid range {MinTitle}-{MaxTitle}",
                title.Length,
                minTitleLength,
                maxTitleLength);
        }

        if (!descriptionValid)
        {
            Log.Warning(
                "Generated description length {DescriptionLength} is outside valid range {MinDescription}-{MaxDescription}",
                description.Length,
                minDescriptionLength,
                maxDescriptionLength);
        }

        return titleValid && descriptionValid;
    }

    private string FormatComponentInfo(MdxComponentDto component)
    {
        var props = component.Properties.Any()
            ? string.Join(", ", component.Properties.Select(p => $"{p.Name}: {p.Type}"))
            : "no props";

        var example = component.Examples.FirstOrDefault() ?? $"<{component.Name} />";

        return $"- {component.Name} ({props}) - Example: {example}";
    }

    private string BuildUserPrompt(string userPrompt, string language)
    {
        return $@"Generate new content in {language} based on this request: {userPrompt}

Remember to return only the JSON structure as specified in the system prompt.";
    }

    private ContentDetailsDto ParseGeneratedContent(string generatedJson)
    {
        try
        {
            using var document = JsonDocument.Parse(generatedJson);

            var root = document.RootElement;

            return new ContentDetailsDto
            {
                Title = GetStringProperty(root, "title"),
                Description = GetStringProperty(root, "description"),
                Body = GetStringProperty(root, "body"),
                Slug = GetStringProperty(root, "slug"),
                Author = GetStringProperty(root, "author"),
                Category = GetStringProperty(root, "category"),
                Tags = GetStringArrayProperty(root, "tags"),
                CoverImageAlt = GetStringProperty(root, "coverImageAlt"),
            };
        }
        catch (JsonException ex)
        {
            Log.Error(ex, "Failed to parse generated content JSON: {Json}", generatedJson);
            throw new AIProviderException("ContentGeneration", $"AI generated invalid JSON content: {ex.Message}", ex);
        }
    }

    private string GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : string.Empty;
    }

    private string[] GetStringArrayProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    result.Add(value);
                }
            }
        }

        return result.ToArray();
    }

    private void AddIfPresent(List<string> items, string label, Dictionary<string, string?> settings, string key)
    {
        if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            items.Add($"{label}: {value.Trim()}");
        }
    }

    private async Task<string> BuildSiteProfileSectionAsync()
    {
        var keys = new[]
        {
            AiSettingKeys.SiteTopic,
            AiSettingKeys.SiteAudience,
            AiSettingKeys.BrandVoice,
            AiSettingKeys.PreferredTerms,
            AiSettingKeys.AvoidTerms,
            AiSettingKeys.StyleExamples,
        };

        var settings = await settingService.GetSettingsByKeysAsync(keys);

        var items = new List<string>();
        AddIfPresent(items, "Topic", settings, AiSettingKeys.SiteTopic);
        AddIfPresent(items, "Audience", settings, AiSettingKeys.SiteAudience);
        AddIfPresent(items, "Voice", settings, AiSettingKeys.BrandVoice);
        AddIfPresent(items, "Preferred terms", settings, AiSettingKeys.PreferredTerms);
        AddIfPresent(items, "Avoid", settings, AiSettingKeys.AvoidTerms);
        AddIfPresent(items, "Style examples", settings, AiSettingKeys.StyleExamples);

        if (items.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", items.Select(i => $"- {i}"));
    }

    private async Task<string> BuildEditSystemPromptAsync()
    {
        // Get content length constraints from settings/configuration
        var (minTitleLength, maxTitleLength, minDescriptionLength, maxDescriptionLength) = await GetContentLengthConstraintsAsync();

        var siteProfileSection = await BuildSiteProfileSectionAsync();

        var prompt = $@"You are a content editor assistant. Your task is to edit existing content based on user prompts while maintaining the original structure and improving quality.

    Guidelines:
    - Preserve the core meaning and structure of the original content
    - Apply the user's requested changes thoughtfully
    - Maintain appropriate tone and style
    - Ensure all content is factual and well-written
    - Keep the same format (markdown, etc.) as the original
    ";

        if (!string.IsNullOrEmpty(siteProfileSection))
        {
            prompt += $@"
    SITE PROFILE:
    {siteProfileSection}
    ";
        }

        prompt += $@"

    CONTENT LENGTH REQUIREMENTS:
    - Title: Must be between {minTitleLength} and {maxTitleLength} characters (SEO optimized)
    - Description: Must be between {minDescriptionLength} and {maxDescriptionLength} characters (SEO optimized for meta descriptions)

    Return your response as valid JSON with these fields:
    - title: Edited article title ({minTitleLength}-{maxTitleLength} characters)
    - slug: URL-friendly slug (lowercase, hyphens instead of spaces)
    - description: Brief summary/meta description ({minDescriptionLength}-{maxDescriptionLength} characters)
    - body: Main content body (preserve markdown formatting)
    - tags: Array of relevant tags
    - category: Relevant category

    Example format:
    {{
      ""title"": ""Updated Article Title"",
      ""slug"": ""updated-article-title"",
      ""description"": ""Brief description of the updated content"",
      ""body"": ""# Main Heading\n\nUpdated content here..."",
      ""tags"": [""tag1"", ""tag2""],
      ""category"": ""category1""
    }}";

        return prompt;
    }

    private string BuildEditUserPrompt(ContentEditRequest contentData, string userPrompt)
    {
        return $@"Please edit the following content based on this request: {userPrompt}

Current Content:
Title: {contentData.Title ?? "[No title]"}
Slug: {contentData.Slug ?? "[No slug]"}
Description: {contentData.Description ?? "[No description]"}
Tags: {string.Join(", ", contentData.Tags ?? Array.Empty<string>())}
Category: {contentData.Category ?? "[No category]"}

Body:
{contentData.Body ?? "[No content]"}

Please provide the edited version in the specified JSON format.";
    }
}
