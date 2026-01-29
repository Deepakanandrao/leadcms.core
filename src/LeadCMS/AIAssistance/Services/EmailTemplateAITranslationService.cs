// <copyright file="EmailTemplateAITranslationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using AutoMapper;
using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;

namespace LeadCMS.Core.AIAssistance.Services;

public class EmailTemplateAITranslationService : IEmailTemplateAITranslationService
{
    private readonly IMapper mapper;
    private readonly ITranslationService translationService;
    private readonly ITextGenerationService textGenerationService;
    private readonly ILanguageValidationService languageValidationService;

    public EmailTemplateAITranslationService(
        IMapper mapper,
        ITranslationService translationService,
        ITextGenerationService textGenerationService,
        ILanguageValidationService languageValidationService)
    {
        this.mapper = mapper;
        this.translationService = translationService;
        this.textGenerationService = textGenerationService;
        this.languageValidationService = languageValidationService;
    }

    public async Task<EmailTemplateDetailsDto> CreateAITranslationDraftAsync(int emailTemplateId, string targetLanguage, int? targetEmailGroupId = null)
    {
        // Validate the language is supported
        languageValidationService.ValidateLanguage(targetLanguage);

        // Get the original email template with KeepOriginal transformer to have all the data
        var originalDraft = await translationService.CreateTranslationDraftAsync<EmailTemplate>(
            emailTemplateId, targetLanguage, TranslationTransformerType.KeepOriginal);

        // Translate the email template fields
        var translatedMetadata = await TranslateEmailTemplateAsync(originalDraft, targetLanguage);

        // Apply translations to the draft
        originalDraft.Name = translatedMetadata.Name;
        originalDraft.Subject = translatedMetadata.Subject;
        originalDraft.BodyTemplate = translatedMetadata.BodyTemplate;
        originalDraft.FromName = translatedMetadata.FromName;

        // Set the target email group if specified, otherwise keep the original group
        if (targetEmailGroupId.HasValue)
        {
            originalDraft.EmailGroupId = targetEmailGroupId.Value;
        }

        // Update source to indicate AI translation
        originalDraft.Source = $"AI translated from {emailTemplateId}";

        // Map to DTO and return
        var translatedDto = mapper.Map<EmailTemplateDetailsDto>(originalDraft);

        Log.Information(
            "Successfully created AI translation draft for EmailTemplate Id={EmailTemplateId} to language {Language} in group {EmailGroupId}",
            emailTemplateId,
            targetLanguage,
            originalDraft.EmailGroupId);

        return translatedDto;
    }

    private static EmailTemplateTranslationMetadata ValidateAndParseMetadataJson(string jsonText)
    {
        try
        {
            // First validate it's valid JSON
            using var document = JsonDocument.Parse(jsonText);

            // Then deserialize to our metadata object
            var metadata = JsonHelper.Deserialize<EmailTemplateTranslationMetadata>(jsonText);

            if (metadata == null)
            {
                throw new InvalidOperationException("Failed to deserialize email template metadata JSON");
            }

            return metadata;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"AI generated invalid JSON for email template metadata: {ex.Message}", ex);
        }
    }

    private async Task<EmailTemplateTranslationMetadata> TranslateEmailTemplateAsync(EmailTemplate emailTemplate, string targetLanguage)
    {
        // Create metadata object for translation
        var metadata = new EmailTemplateTranslationMetadata
        {
            Name = emailTemplate.Name,
            Subject = emailTemplate.Subject,
            BodyTemplate = emailTemplate.BodyTemplate,
            FromName = emailTemplate.FromName,
        };

        var metadataJson = JsonHelper.Serialize(metadata);

        var systemPrompt =
$@"You are a professional translator for an AI-powered CMS, specializing in email template translation. Translate the prompted JSON object containing email template data to {targetLanguage}.

CRITICAL RULES - STRICT STRUCTURE PRESERVATION:
1. Return ONLY valid JSON with the EXACT same structure as the input
2. Translate all human-readable text values to {targetLanguage}
3. Keep all JSON property names unchanged - do not translate keys
4. For 'Name': Translate the descriptive part but keep technical identifiers if present
5. For 'Subject': Translate naturally while maintaining the email subject line tone
6. For 'BodyTemplate':
   - Preserve ALL HTML tags, attributes, and inline CSS styles EXACTLY as they appear
   - Use ONLY ${{token}} format for variables and placeholders (e.g., ${{name}}, ${{email}}, ${{company}})
   - Convert any other placeholder formats (<%token%>, {{token}}, {{{{token}}}}, HTML-encoded) to ${{token}} format
   - Translate ONLY the readable text content between HTML tags
   - DO NOT modify table structures, CSS properties, or HTML attributes
   - Maintain email client compatibility by preserving inline styles
7. For 'FromName': Translate to natural name in {targetLanguage}
8. If a field is empty or null, keep it exactly as is
9. Ensure the output is valid, parseable JSON

EMAIL HTML PRESERVATION RULES - DO NOT MODIFY:
- All table-based layouts (tables are critical for email client compatibility)
- Inline CSS styles (style=""..."" attributes)
- HTML structure, widths, colors, fonts, or spacing
- All cellpadding, cellspacing, border attributes
- Responsive design elements and media queries

DO NOT:
- Add new HTML elements or attributes
- Remove existing HTML elements or attributes
- Change CSS property values
- Modify the structure or nesting of HTML elements

PLACEHOLDER FORMAT STANDARDIZATION:
- Convert ALL variable formats to ${{token}} syntax (dollar sign + curly braces)
- Replace <%token%> with ${{token}}
- Replace {{token}} with ${{token}}
- Replace {{{{token}}}} with ${{token}}
- Replace HTML-encoded versions (&lt;%token%&gt;) with ${{token}}
- Maintain the semantic meaning of variables when converting formats";

        var request = new TextGenerationRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = metadataJson,
        };

        try
        {
            var response = await textGenerationService.GenerateTextAsync(request);

            // Validate and parse the JSON response
            var translatedMetadata = ValidateAndParseMetadataJson(response.GeneratedText);

            Log.Information("Successfully translated email template metadata to {Language}", targetLanguage);
            return translatedMetadata;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to translate email template metadata to {Language}, falling back to original", targetLanguage);
            return metadata; // Fallback to original if translation fails
        }
    }
}