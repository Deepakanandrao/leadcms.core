// <copyright file="EmailTemplateGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using AutoMapper;
using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Exceptions;
using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.AIAssistance.Services;

public class EmailTemplateGenerationService : IEmailTemplateGenerationService
{
    private const string EditSystemPrompt = @"You are an email template editor assistant for an AI-powered CMS. Your task is to edit existing email templates based on user prompts while strictly maintaining HTML structure and email best practices.

CRITICAL RULES - READ CAREFULLY:
1. PRESERVE STRUCTURE: Keep the exact same HTML structure and layout as the original template
2. NO HALLUCINATION: Do not add new HTML elements, CSS properties, or structures not present in the original
3. CONSERVATIVE EDITS: When the request is ambiguous, make the minimum changes necessary

EMAIL HTML REQUIREMENTS:
- Generate ONLY the email body content - DO NOT include <html>, <head>, or <body> wrapper tags
- Use ONLY inline CSS styles (style=""..."") - email clients don't support external stylesheets or <style> blocks
- Use table-based layouts for maximum email client compatibility (div layouts often break)
- Set explicit width, height, padding, margin, and color properties inline
- Use web-safe fonts: Arial, Helvetica, Times New Roman, Georgia, Verdana
- Always include alt text for images and use absolute URLs for image sources
- Keep line length under 600px width for optimal rendering
- Use background-color instead of background shorthand property
- Avoid CSS properties like: position, float, z-index, flexbox, grid (poorly supported)

EMAIL TEMPLATE GUIDELINES:
- Use ONLY ${token} format for all variables and placeholders (e.g., ${name}, ${email}, ${company})
- Convert any other placeholder formats like <%token%>, {{token}}, {{{{token}}}}, or HTML-encoded versions to ${token} format
- Apply the user's requested changes thoughtfully while preserving the original structure
- Ensure the template is mobile-friendly with responsive design
- Keep appropriate tone for email communication
- Preserve sender information format
- Test-friendly structure with clear content hierarchy

OUTPUT FORMAT - Return ONLY valid JSON with this exact structure:
{
  ""name"": ""Template_Name"",
  ""subject"": ""Email Subject Line"",
  ""bodyTemplate"": ""<table style='...'><tr><td>Content with ${variables}</td></tr></table>"",
  ""fromName"": ""Sender Name""
}";

    private readonly PgDbContext dbContext;
    private readonly ITextGenerationService textGenerationService;
    private readonly IMapper mapper;

    public EmailTemplateGenerationService(
        PgDbContext dbContext,
        ITextGenerationService textGenerationService,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.textGenerationService = textGenerationService;
        this.mapper = mapper;
    }

    public async Task<EmailTemplateDetailsDto> GenerateEmailTemplateAsync(EmailTemplateGenerationRequest request)
    {
        Log.Information("Starting email template generation for group {EmailGroupId} in language {Language}", request.EmailGroupId, request.Language);

        // Step 1: Validate email group exists
        var emailGroup = await dbContext.EmailGroups!
            .FirstOrDefaultAsync(eg => eg.Id == request.EmailGroupId);

        if (emailGroup == null)
        {
            throw new AIProviderException("EmailTemplateGeneration", $"Email group with ID {request.EmailGroupId} not found");
        }

        // Step 2: Find sample email templates
        var sampleTemplate = await FindSampleEmailTemplateAsync(request.EmailGroupId, request.Language);

        if (sampleTemplate == null)
        {
            throw new AIProviderException(
                "EmailTemplateGeneration",
                "Not enough data in the database for the AI assistant to generate new email templates. Please create at least one email template in this group first.");
        }

        // Step 3: Build prompts and generate email template
        var systemPrompt = BuildSystemPrompt(sampleTemplate);
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
            var generatedTemplate = ParseGeneratedEmailTemplate(response.GeneratedText);

            // Create an EmailTemplate entity with the generated data
            var emailTemplateEntity = new EmailTemplate
            {
                Name = generatedTemplate.Name,
                Subject = generatedTemplate.Subject,
                BodyTemplate = generatedTemplate.BodyTemplate,
                FromName = generatedTemplate.FromName,
                FromEmail = sampleTemplate.FromEmail, // Keep the same from email as sample
                Language = request.Language,
                EmailGroupId = request.EmailGroupId,
                TranslationKey = null, // New generated content gets a new translation key via translation service
                Source = $"AI Generated - Model: {response.Model}, Tokens: {response.TokensUsed}",
            };

            // Map to EmailTemplateDetailsDto
            var result = mapper.Map<EmailTemplateDetailsDto>(emailTemplateEntity);

            Log.Information("Successfully generated email template for group {EmailGroupId} in language {Language}", request.EmailGroupId, request.Language);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate email template for group {EmailGroupId} in language {Language}", request.EmailGroupId, request.Language);
            throw new AIProviderException("EmailTemplateGeneration", "Failed to generate email template", ex);
        }
    }

    public async Task<EmailTemplateDetailsDto> GenerateEmailTemplateEditAsync(EmailTemplateEditRequest request)
    {
        Log.Information("Starting email template editing with prompt: {Prompt}", request.Prompt);

        // Create the editing prompt
        var currentTemplate = new EmailTemplateTranslationMetadata
        {
            Name = request.Name ?? string.Empty,
            Subject = request.Subject ?? string.Empty,
            BodyTemplate = request.BodyTemplate ?? string.Empty,
            FromName = request.FromName ?? string.Empty,
        };

        var currentTemplateJson = JsonHelper.Serialize(currentTemplate);
        var userPrompt = $"Current email template:\n{currentTemplateJson}\n\nUser's editing request: {request.Prompt}";

        var textRequest = new TextGenerationRequest
        {
            SystemPrompt = EditSystemPrompt,
            UserPrompt = userPrompt,
        };

        try
        {
            var response = await textGenerationService.GenerateTextAsync(textRequest);

            // Parse the generated JSON content
            var editedTemplate = ParseGeneratedEmailTemplate(response.GeneratedText);

            // Create an EmailTemplate entity with the edited data
            var emailTemplateEntity = new EmailTemplate
            {
                Name = editedTemplate.Name,
                Subject = editedTemplate.Subject,
                BodyTemplate = editedTemplate.BodyTemplate,
                FromName = editedTemplate.FromName,
                FromEmail = request.FromEmail ?? string.Empty,
                Language = request.Language ?? string.Empty,
                TranslationKey = request.TranslationKey,
                EmailGroupId = request.EmailGroupId ?? 0,
                Source = $"AI Edited - Model: {response.Model}, Tokens: {response.TokensUsed}",
            };

            // Map to EmailTemplateDetailsDto
            var result = mapper.Map<EmailTemplateDetailsDto>(emailTemplateEntity);

            Log.Information("Successfully edited email template");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to edit email template");
            throw new AIProviderException("EmailTemplateEditing", "Failed to edit email template", ex);
        }
    }

    private static string BuildSystemPrompt(EmailTemplate sampleTemplate)
    {
        return $@"You are an AI assistant for an AI-powered CMS, specialized in creating email templates. Generate a new email template that precisely matches the structure and style of the provided sample.

SAMPLE EMAIL TEMPLATE (use this as your template - match its structure exactly):
Name: {sampleTemplate.Name}
Subject: {sampleTemplate.Subject}
From Name: {sampleTemplate.FromName}
Body Template:
{sampleTemplate.BodyTemplate}

CRITICAL RULES - READ CAREFULLY:
1. MATCH THE SAMPLE: Generate an email template with the SAME structure, layout, and HTML patterns as the sample
2. NO HALLUCINATION: Do not add new HTML elements, CSS properties, or structures not demonstrated in the sample
3. REUSE PATTERNS: Only use HTML patterns, table structures, and CSS styles that appear in the sample template
4. PRESERVE STYLE: Match the visual style, colors, fonts, and spacing of the sample

EMAIL HTML REQUIREMENTS:
1. Generate ONLY the email body content - DO NOT include <html>, <head>, or <body> wrapper tags
2. Use ONLY inline CSS styles (style=""..."") - email clients don't support external stylesheets or <style> blocks
3. Use table-based layouts for maximum email client compatibility (div layouts often break in email)
4. Set explicit width, height, padding, margin, and color properties inline on each element
5. Use web-safe fonts only: Arial, Helvetica, Times New Roman, Georgia, Verdana, sans-serif
6. Always include alt text for images and use absolute URLs for image sources
7. Keep total width under 600px for optimal rendering across email clients
8. Use background-color instead of background shorthand property (better support)
9. Avoid CSS properties poorly supported in email: position, float, z-index, flexbox, grid, transform
10. Use cellpadding/cellspacing=""0"" on tables and border=""0"" to avoid unwanted spacing

EMAIL TEMPLATE BEST PRACTICES:
- Create professional, well-structured email templates with clear hierarchy
- Use ONLY ${{token}} format for all variables and placeholders (e.g., ${{name}}, ${{email}}, ${{company}})
- Convert any other placeholder formats (<%token%>, {{token}}, {{{{token}}}}, HTML-encoded versions) to ${{token}} format
- Follow the structure and style of the sample template exactly
- Include proper fallback colors and fonts

PLACEHOLDER FORMAT REQUIREMENTS:
- ALL variables must use ${{token}} syntax (dollar sign + curly braces)
- Examples: ${{firstName}}, ${{lastName}}, ${{companyName}}, ${{productName}}, ${{unsubscribeLink}}
- Replace any <%token%>, {{token}}, {{{{token}}}}, or encoded formats with ${{token}}

OUTPUT FORMAT - Return ONLY valid JSON with this exact structure:
{{
  ""name"": ""Template_Name"",
  ""subject"": ""Email Subject Line"",
  ""bodyTemplate"": ""HTML content matching sample structure"",
  ""fromName"": ""Sender Name""
}}

The bodyTemplate should contain ONLY the email content without html/head/body wrapper tags.";
    }

    private static string BuildUserPrompt(string prompt, string language)
    {
        return $@"Create an email template in {language} language based on this request:

{prompt}

IMPORTANT REMINDERS:
- Match the structure and style of the sample template exactly
- Do not add HTML elements or CSS properties not present in the sample
- Use table-based layout with inline CSS styles only
- Use ONLY ${{token}} format for all placeholders (e.g., ${{firstName}}, ${{email}}, ${{companyName}})
- NO <html>, <head>, or <body> tags - content only
- Return only the JSON structure as specified in the system prompt";
    }

    private static EmailTemplateTranslationMetadata ParseGeneratedEmailTemplate(string jsonText)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonText);
            var template = JsonHelper.Deserialize<EmailTemplateTranslationMetadata>(jsonText);

            if (template == null)
            {
                throw new InvalidOperationException("Failed to deserialize generated email template JSON");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(template.Name))
            {
                throw new InvalidOperationException("Generated email template is missing required 'name' field");
            }

            if (string.IsNullOrWhiteSpace(template.Subject))
            {
                throw new InvalidOperationException("Generated email template is missing required 'subject' field");
            }

            if (string.IsNullOrWhiteSpace(template.BodyTemplate))
            {
                throw new InvalidOperationException("Generated email template is missing required 'bodyTemplate' field");
            }

            return template;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AI generated invalid JSON for email template: {ex.Message}. JSON: {jsonText}", ex);
        }
    }

    private async Task<EmailTemplate?> FindSampleEmailTemplateAsync(int emailGroupId, string language)
    {
        // Try to find a template in the same group and language
        var sampleInLanguage = await dbContext.EmailTemplates!
            .Where(et => et.EmailGroupId == emailGroupId && et.Language == language)
            .FirstOrDefaultAsync();

        if (sampleInLanguage != null)
        {
            return sampleInLanguage;
        }

        // Fallback to any template in the same group
        var sampleInGroup = await dbContext.EmailTemplates!
            .Where(et => et.EmailGroupId == emailGroupId)
            .FirstOrDefaultAsync();

        return sampleInGroup;
    }
}