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
    private readonly IEmailGroupResolutionService emailGroupResolutionService;

    public EmailTemplateAITranslationService(
        IMapper mapper,
        ITranslationService translationService,
        ITextGenerationService textGenerationService,
        ILanguageValidationService languageValidationService,
        IEmailGroupResolutionService emailGroupResolutionService)
    {
        this.mapper = mapper;
        this.translationService = translationService;
        this.textGenerationService = textGenerationService;
        this.languageValidationService = languageValidationService;
        this.emailGroupResolutionService = emailGroupResolutionService;
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

        // Set the target email group if specified, otherwise try to find the matching group in the target language
        if (targetEmailGroupId.HasValue)
        {
            originalDraft.EmailGroupId = targetEmailGroupId.Value;
        }
        else
        {
            originalDraft.EmailGroupId = await emailGroupResolutionService.ResolveTargetEmailGroupIdAsync(originalDraft.EmailGroupId, targetLanguage);
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

        var formatLabel = "HTML";

        var systemPrompt =
$@"You are a professional translator for an AI-powered CMS, specializing in email template translation. Translate the prompted JSON object containing email template data to {targetLanguage}.

The template body is in {formatLabel} format.

CRITICAL RULES - STRICT STRUCTURE PRESERVATION:
1. Return ONLY valid JSON with the EXACT same structure as the input
2. Translate all human-readable text values to {targetLanguage}
3. Keep all JSON property names unchanged - do not translate keys
4. For 'Name': DO NOT translate — this value is used as a localisation key and must be preserved exactly as-is
5. For 'Subject': Translate naturally while maintaining the email subject line tone
6. For 'BodyTemplate':
   - Preserve ALL markup tags, attributes, and styles EXACTLY as they appear
   - The format is {formatLabel} — preserve all {formatLabel}-specific components and attributes
   - Use ONLY {{{{ token }}}} Liquid syntax for variables and placeholders (e.g., {{{{ name }}}}, {{{{ email }}}}, {{{{ company }}}}, {{{{ unsubscribeUrl }}}})
   - Preserve ALL Liquid tags exactly: {{{{ variable }}}}, {{% if cond %}}...{{% endif %}}, {{% for item in list %}}...{{% endfor %}}
   - Convert any legacy placeholder formats (<%token%>, ${{token}}, HTML-encoded) to {{{{ token }}}} Liquid syntax
   - Translate ONLY the readable text content between tags
   - DO NOT modify component structures, CSS properties, or attributes
7. For 'FromName': Translate to natural name in {targetLanguage}
8. For 'Format': Keep the value exactly as is — do not change or translate it
9. If a field is empty or null, keep it exactly as is
10. Ensure the output is valid, parseable JSON

MARKUP PRESERVATION RULES - DO NOT MODIFY:
- All layout structures (tables for HTML)
- Inline CSS styles
- Component nesting and structure
- All spacing, color, font, and sizing values
- All responsive design elements

DO NOT:
- Add new elements or attributes
- Remove existing elements or attributes
- Change CSS attribute values
- Modify the structure or nesting of elements
- Change the format field

PLACEHOLDER FORMAT STANDARDISATION:
- ALL variable placeholders must use Liquid {{{{ token }}}} syntax (double curly braces)
- Preserve ALL Liquid control tags exactly: {{% if %}}, {{% unless %}}, {{% for %}}, {{% endif %}}, {{% endfor %}}, etc.
- Replace <%token%> with {{{{ token }}}}
- Replace ${{token}} with {{{{ token }}}}
- Replace HTML-encoded versions (&lt;%token%&gt;) with {{{{ token }}}}
- Translate text content ONLY — do not modify any Liquid syntax or tag attributes";

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