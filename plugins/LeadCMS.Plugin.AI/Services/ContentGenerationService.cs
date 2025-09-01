// <copyright file="ContentGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Plugin.AI.DTOs;
using LeadCMS.Plugin.AI.Exceptions;
using LeadCMS.Plugin.AI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace LeadCMS.Plugin.AI.Services;

public class ContentGenerationService : IContentGenerationService
{
    private readonly PgDbContext dbContext;
    private readonly ITextGenerationService textGenerationService;
    private readonly IMdxComponentParserService mdxComponentParserService;
    private readonly IMapper mapper;

    public ContentGenerationService(
        PgDbContext dbContext,
        ITextGenerationService textGenerationService,
        IMdxComponentParserService mdxComponentParserService,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.textGenerationService = textGenerationService;
        this.mdxComponentParserService = mdxComponentParserService;
        this.mapper = mapper;
    }

    public async Task<ContentDetailsDto> GenerateContentAsync(ContentGenerationRequest request)
    {
        Log.Information("Starting content generation for type {ContentType} in language {Language}", request.ContentType, request.Language);

        // Step 1: Validate content type exists
        var contentType = await dbContext.ContentTypes!
            .FirstOrDefaultAsync(ct => ct.Uid == request.ContentType);

        if (contentType == null)
        {
            throw new ArgumentException($"Content type '{request.ContentType}' not found", nameof(request));
        }

        // Step 2: Find sample content records
        var sampleContent = await FindSampleContentAsync(request.ContentType, request.Language);

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
        var systemPrompt = BuildSystemPrompt(contentType, sampleContent, componentAnalysis);
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

            // Create a Content entity with the generated data
            var contentEntity = new Content
            {
                Title = generatedContent.Title,
                Description = generatedContent.Description,
                Body = generatedContent.Body,
                Slug = generatedContent.Slug,
                Author = generatedContent.Author ?? sampleContent.Author,
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

    private async Task<Content?> FindSampleContentAsync(string contentType, string language)
    {
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

    private string BuildSystemPrompt(ContentType contentType, Content sampleContent, MdxComponentAnalysisDto? componentAnalysis)
    {
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

        if (componentAnalysis != null && componentAnalysis.Components.Any())
        {
            prompt += $@"

MDX COMPONENTS AVAILABLE:
This content type supports the following MDX components. You can use these in the body content:
{string.Join("\n", componentAnalysis.Components.Select(c => FormatComponentInfo(c)))}";
        }

        prompt += $@"

REQUIREMENTS:
1. Return ONLY valid JSON with the exact structure shown below
2. Generate original, high-quality content that matches the style and format of the sample
3. Ensure the body content is in {contentType.Format} format
4. Generate an appropriate slug (URL-friendly, lowercase, hyphen-separated)
5. Keep the same author, category structure, and tagging style as the sample
6. If using MDX format, you may include the available components listed above

REQUIRED JSON STRUCTURE:
{{
  ""title"": ""Generated title"",
  ""description"": ""Generated description"",
  ""body"": ""Generated body content in {contentType.Format} format"",
  ""slug"": ""url-friendly-slug"",
  ""author"": ""Author name"",
  ""category"": ""Category name"",
  ""tags"": [""tag1"", ""tag2""],
  ""coverImageAlt"": ""Alt text for cover image""
}}";

        return prompt;
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
}
