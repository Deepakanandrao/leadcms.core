// <copyright file="EmailTemplateGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using System.Text.Json;
using AutoMapper;
using LeadCMS.Core.AIAssistance.DTOs;
using LeadCMS.Core.AIAssistance.Exceptions;
using LeadCMS.Core.AIAssistance.Interfaces;
using LeadCMS.Data;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Exceptions;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.AIAssistance.Services;

public class EmailTemplateGenerationService : IEmailTemplateGenerationService
{
    private const string HtmlToMjmlConversionPrompt = @"You are an expert at converting HTML email templates to MJML (MailJet Markup Language). Convert the provided HTML email template to valid, well-structured MJML that preserves the original visual appearance as closely as possible.

MJML STRUCTURE REQUIREMENTS:
- The output MUST be a complete MJML document starting with <mjml> and ending with </mjml>
- Use <mj-head> for metadata, shared styles, and font declarations
- Use <mj-body> as the container for all visible content
- Use <mj-section> for horizontal rows (replaces table rows)
- Use <mj-column> inside sections for column-based layouts
- Sections can contain 1-4 columns; column widths are percentages (e.g. width=""50%"")

COMPONENT MAPPING GUIDE:
- HTML <table>/<tr>/<td> layouts → <mj-section>/<mj-column>
- HTML <p>, <span>, <h1>-<h6>, text blocks → <mj-text>
- HTML <img> → <mj-image src=""..."" width=""..."" alt=""..."" />
- HTML <a> styled as button → <mj-button href=""..."">
- HTML <hr> / dividers → <mj-divider border-color=""..."" border-width=""..."" />
- Empty spacing / padding rows → <mj-spacer height=""..."" />
- HTML <table> for data → <mj-table> (preserves raw table markup)
- Social media links → <mj-social> / <mj-social-element>
- Navbar / menu → <mj-navbar> / <mj-navbar-link>

STYLING RULES:
- Move shared styles to <mj-attributes> in <mj-head> for DRY approach:
  <mj-attributes>
    <mj-all font-family=""Arial, sans-serif"" />
    <mj-text font-size=""14px"" line-height=""22px"" color=""#333333"" />
    <mj-button background-color=""#007bff"" color=""#ffffff"" border-radius=""4px"" font-weight=""600"" />
  </mj-attributes>
- Use <mj-style> for CSS overrides and responsive media queries:
  <mj-style>
    .custom-class { color: #333; }
  </mj-style>
- Use <mj-font name=""FontName"" href=""https://fonts.googleapis.com/..."" /> for custom fonts
- Set colors, fonts, padding via MJML attributes (font-size, color, background-color, padding, etc.)
- Use web-safe fonts: Arial, Helvetica, Times New Roman, Georgia, Verdana
- Padding format: padding=""top right bottom left"" or padding-top, padding-bottom, etc.

MJML COMPONENT REFERENCE WITH EXAMPLES:

1. Sections and Columns (layout building blocks):
   <mj-section background-color=""#ffffff"" padding=""20px 0"">
     <mj-column width=""50%"">
       <mj-text>Left column</mj-text>
     </mj-column>
     <mj-column width=""50%"">
       <mj-text>Right column</mj-text>
     </mj-column>
   </mj-section>

2. Text content:
   <mj-text font-size=""20px"" font-weight=""600"" color=""#2d3748"" align=""center"" padding=""10px 25px"">
     Heading text
   </mj-text>

3. Images:
   <mj-image src=""https://example.com/image.jpg"" width=""600px"" alt=""Alt text"" border-radius=""8px"" />

4. Buttons:
   <mj-button href=""https://example.com"" background-color=""#007bff"" color=""#ffffff"" border-radius=""4px"" font-size=""16px"" padding=""15px 25px"">
     Click Here
   </mj-button>

5. Dividers:
   <mj-divider border-color=""#cbd5e0"" border-width=""1px"" padding=""10px 25px"" />

6. Spacers:
   <mj-spacer height=""20px"" />

7. Full-width sections:
   <mj-section full-width=""full-width"" background-color=""#f4f4f4"">
     <mj-column>
       <mj-text>Full width content</mj-text>
     </mj-column>
   </mj-section>

8. Preview text:
   <mj-preview>This text appears in email client previews</mj-preview>

9. Title:
   <mj-title>Email Subject in Code</mj-title>

LIQUID TEMPLATE VARIABLES:
- Preserve all {{ variableName }} Liquid placeholders exactly as they appear
- Preserve all {% if condition %}...{% endif %} and {% for %}...{% endfor %} blocks
- Preserve all {% unless condition %}...{% endunless %} blocks
- Convert any legacy placeholders (<%token%>, ${token}) to {{ token }} Liquid syntax

CRITICAL RULES:
1. Output ONLY the MJML markup — no explanations, no JSON wrapping, no markdown
2. Preserve all visual styles: colors, fonts, spacing, borders, background colors
3. Preserve all links, images, and interactive elements
4. Preserve all Liquid template variables and conditional blocks exactly
5. Ensure the MJML is valid and will compile without errors
6. Do NOT add content that doesn't exist in the original HTML
7. MJML handles responsive design natively — do not add manual media queries unless the original has specific responsive breakpoints";

    private readonly PgDbContext dbContext;
    private readonly ITextGenerationService textGenerationService;
    private readonly IMjmlRenderingService mjmlRenderingService;
    private readonly IMapper mapper;

    public EmailTemplateGenerationService(
        PgDbContext dbContext,
        ITextGenerationService textGenerationService,
        IMjmlRenderingService mjmlRenderingService,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.textGenerationService = textGenerationService;
        this.mjmlRenderingService = mjmlRenderingService;
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

        // Step 2: Find sample email template
        var sampleTemplate = await FindSampleEmailTemplateAsync(request.EmailGroupId, request.Language, request.ReferenceEmailTemplateId);

        if (sampleTemplate == null)
        {
            throw new AIProviderException(
                "EmailTemplateGeneration",
                "Not enough data in the database for the AI assistant to generate new email templates. Please create at least one email template in this group first.");
        }

        // Step 3: Determine target format — use requested format if provided, otherwise sample's format
        var targetFormat = request.Format ?? sampleTemplate.Format;

        // Step 4: Build prompts and generate email template
        var systemPrompt = BuildSystemPrompt(sampleTemplate, targetFormat);
        var userPrompt = BuildUserPrompt(request.Prompt, request.Language, request.TemplateVariables);

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
                Format = targetFormat,
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

        var currentBodyTemplate = request.BodyTemplate ?? string.Empty;
        var currentFormat = request.Format ?? EmailTemplateFormat.Html;

        var currentTemplate = new EmailTemplateTranslationMetadata
        {
            Name = request.Name ?? string.Empty,
            Subject = request.Subject ?? string.Empty,
            BodyTemplate = currentBodyTemplate,
            FromName = request.FromName ?? string.Empty,
            Format = currentFormat,
        };

        var currentTemplateJson = JsonHelper.Serialize(currentTemplate);

        var additionalParamsSection = BuildTemplateVariablesSection(request.TemplateVariables);

        var referenceSection = string.Empty;
        if (request.ReferenceEmailTemplateId.HasValue)
        {
            var referenceTemplate = await dbContext.EmailTemplates!
                .FirstOrDefaultAsync(et => et.Id == request.ReferenceEmailTemplateId.Value);

            if (referenceTemplate == null)
            {
                throw new AIProviderException(
                    "EmailTemplateEditing",
                    $"Reference email template with ID {request.ReferenceEmailTemplateId.Value} was not found.");
            }

            referenceSection = $"\n\nREFERENCE SAMPLE (use as visual / structural guide):\n{referenceTemplate.BodyTemplate}";
        }

        var userPrompt = $"Current email template:\n{currentTemplateJson}\n\nUser's editing request: {request.Prompt}{additionalParamsSection}{referenceSection}";

        var textRequest = new TextGenerationRequest
        {
            SystemPrompt = BuildEditSystemPrompt(currentFormat),
            UserPrompt = userPrompt,
        };

        try
        {
            var response = await textGenerationService.GenerateTextAsync(textRequest);

            // Parse the generated JSON content
            var editedTemplate = ParseGeneratedEmailTemplate(response.GeneratedText);

            // Create an EmailTemplate entity with the edited data — preserve original format
            var emailTemplateEntity = new EmailTemplate
            {
                Name = editedTemplate.Name,
                Subject = editedTemplate.Subject,
                BodyTemplate = editedTemplate.BodyTemplate,
                Format = currentFormat, // Always preserve the original format
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

    /// <inheritdoc/>
    public async Task<EmailTemplateConvertFormatResponse> ConvertFormatAsync(EmailTemplateConvertFormatRequest request)
    {
        if (request.CurrentFormat == request.TargetFormat)
        {
            throw new BadRequestException("Source and target formats are the same. No conversion needed.");
        }

        if (request.TargetFormat == EmailTemplateFormat.Html)
        {
            // MJML → HTML: straightforward programmatic compilation
            Log.Information("Converting email template from MJML to HTML via MJML compiler");
            var html = mjmlRenderingService.RenderToHtml(request.BodyTemplate);

            return new EmailTemplateConvertFormatResponse
            {
                BodyTemplate = html,
                Format = EmailTemplateFormat.Html,
                AiPowered = false,
            };
        }

        // HTML → MJML: requires AI
        Log.Information("Converting email template from HTML to MJML via AI");

        var textRequest = new TextGenerationRequest
        {
            SystemPrompt = HtmlToMjmlConversionPrompt,
            UserPrompt = request.BodyTemplate,
        };

        try
        {
            var response = await textGenerationService.GenerateTextAsync(textRequest);
            var mjml = StripMarkdownCodeFences(response.GeneratedText);

            Log.Information("Successfully converted HTML to MJML via AI");

            return new EmailTemplateConvertFormatResponse
            {
                BodyTemplate = mjml,
                Format = EmailTemplateFormat.Mjml,
                AiPowered = true,
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to convert HTML to MJML via AI");
            throw new AIProviderException("EmailTemplateConversion", "Failed to convert HTML email template to MJML. Ensure AI capabilities are enabled.", ex);
        }
    }

    private static string GetFormatSpecificRules(EmailTemplateFormat format)
    {
        return format == EmailTemplateFormat.Mjml
            ? @"FORMAT RULES (MJML):
1. The bodyTemplate MUST be a complete MJML document starting with <mjml> and ending with </mjml>
2. Use standard MJML components for layout and content (see component reference below)
3. Use <mj-attributes> in <mj-head> for shared styles (DRY approach)
4. Use <mj-style> for custom CSS overrides and responsive media queries
5. Set colors, fonts, spacing via MJML attributes (font-size, color, background-color, padding, etc.)
6. MJML handles responsive design natively — leverage this, avoid manual media queries
7. Use web-safe fonts: Arial, Helvetica, Times New Roman, Georgia, Verdana

MJML DOCUMENT STRUCTURE:
<mjml>
  <mj-head>
    <mj-title>Email Title</mj-title>
    <mj-preview>Preview text shown in email clients</mj-preview>
    <mj-font name=""CustomFont"" href=""https://fonts.googleapis.com/css?family=CustomFont"" />
    <mj-attributes>
      <mj-all font-family=""Arial, sans-serif"" />
      <mj-text font-size=""14px"" line-height=""22px"" color=""#333333"" />
      <mj-button background-color=""#007bff"" color=""#ffffff"" border-radius=""4px"" font-weight=""600"" />
    </mj-attributes>
    <mj-style>
      .custom-class { color: #333; }
    </mj-style>
  </mj-head>
  <mj-body background-color=""#f4f5f7"">
    <!-- sections go here -->
  </mj-body>
</mjml>

MJML COMPONENT REFERENCE:
- <mj-section>: Horizontal row container. Attributes: background-color, padding, full-width=""full-width"", border-radius.
- <mj-column>: Column inside section (1-4 per section). Attributes: width (percentage, e.g. ""50%""), padding, background-color, border-radius.
- <mj-text>: Text content (supports inline HTML: <p>, <h1>-<h6>, <span>, <a>). Attributes: font-size, font-weight, color, align, padding, line-height.
- <mj-image>: Image element. Attributes: src, width, alt, border-radius, padding, href (makes clickable). Self-closing tag.
- <mj-button>: Call-to-action button. Attributes: href, background-color, color, border-radius, font-size, font-weight, padding, inner-padding.
- <mj-divider>: Horizontal line separator. Attributes: border-color, border-width, padding. Self-closing tag.
- <mj-spacer>: Vertical spacing. Attributes: height. Self-closing tag.
- <mj-table>: Raw HTML table for data grids. Supports standard <tr>/<td> content inside.
- <mj-social>/<mj-social-element>: Social media links with icons. Attributes: name (facebook-noshare, twitter, linkedin, etc.), href.
- <mj-navbar>/<mj-navbar-link>: Navigation menu bar. Attributes: href, color, font-size.
- <mj-wrapper>: Groups multiple sections with shared background.
- <mj-hero>: Hero section with background image. Attributes: background-color, background-url, mode=""fluid-height"".
- <mj-raw>: Inject raw HTML directly (use sparingly for advanced cases).

MJML LAYOUT EXAMPLES:
Single column:
  <mj-section><mj-column><mj-text>Content</mj-text></mj-column></mj-section>
Two columns:
  <mj-section><mj-column width=""50%""><mj-text>Left</mj-text></mj-column><mj-column width=""50%""><mj-text>Right</mj-text></mj-column></mj-section>
Three columns:
  <mj-section><mj-column width=""33.33%"">...</mj-column><mj-column width=""33.33%"">...</mj-column><mj-column width=""33.33%"">...</mj-column></mj-section>
Full-width background:
  <mj-section full-width=""full-width"" background-color=""#f4f4f4""><mj-column>...</mj-column></mj-section>

STYLING BEST PRACTICES:
- Padding format: padding=""top right bottom left"" or use padding-top, padding-bottom, etc.
- Use <mj-attributes> to define shared styles once instead of repeating on each component
- Use <mj-font> for Google Fonts or custom web fonts, with web-safe fallbacks
- Use <mj-preview> for inbox preview text that appears before the email is opened"
            : @"FORMAT RULES (HTML):
1. The bodyTemplate MUST be standard, well-formed HTML suitable for email clients
2. Use table-based layouts for maximum cross-client compatibility
3. Use inline CSS styles for reliable rendering across email clients
4. Structure: Use <table>, <tr>, <td> for layout; avoid <div>-based layouts
5. Set colors, fonts, spacing via inline style attributes
6. Include proper email DOCTYPE and meta tags if producing a full document";
    }

    private static string GetFormatLabel(EmailTemplateFormat format)
    {
        return format == EmailTemplateFormat.Mjml ? "MJML" : "HTML";
    }

    private static string BuildEditSystemPrompt(EmailTemplateFormat format)
    {
        var formatLabel = GetFormatLabel(format);
        var formatRules = GetFormatSpecificRules(format);

        return $@"You are an email template editor assistant for an AI-powered CMS. Your task is to edit existing {formatLabel} email templates based on user prompts.

CRITICAL RULES - READ CAREFULLY:
1. PRESERVE STRUCTURE: Keep the same logical structure and layout as the original template
2. NO HALLUCINATION: Do not add components or structures not needed by the user's request
3. CONSERVATIVE EDITS: When the request is ambiguous, make the minimum changes necessary
4. OUTPUT MUST BE {formatLabel}: The bodyTemplate must remain valid {formatLabel}

{formatRules}

GENERAL:
- Use web-safe fonts: Arial, Helvetica, Times New Roman, Georgia, Verdana

LIQUID TEMPLATING SYNTAX (use inside text/attribute nodes as needed):
- Variables:     {{{{ variableName }}}}                          e.g. {{{{ firstName }}}}, {{{{ unsubscribeUrl }}}}
- Conditionals: {{% if condition %}}...{{% endif %}}             e.g. {{% if isVip %}}VIP content{{% endif %}}
                {{% unless condition %}}...{{% endunless %}}     e.g. {{% unless unsubscribed %}}show footer{{% endunless %}}
- Loops:        {{% for item in items %}}...{{% endfor %}}       e.g. {{% for product in products %}}{{{{ product.name }}}}{{% endfor %}}
- Convert any legacy placeholder formats (<%token%>, ${{token}}, HTML-encoded) to {{{{ token }}}} Liquid syntax

EMAIL TEMPLATE GUIDELINES:
- Ensure the template is mobile-friendly
- Keep appropriate tone for email communication
- Preserve sender information format
- Clear content hierarchy with proper spacing

OUTPUT FORMAT - Return ONLY valid JSON with this exact structure:
{{
  ""name"": ""Template_Name"",
  ""subject"": ""Email Subject Line"",
  ""bodyTemplate"": ""<the template body in {formatLabel} format>"",
  ""fromName"": ""Sender Name"",
  ""format"": ""{formatLabel}""
}}

IMPORTANT: The 'name' field is used as a localisation key and must NEVER be translated. Keep the original template name exactly as-is.";
    }

    private static string BuildSystemPrompt(EmailTemplate sampleTemplate, EmailTemplateFormat targetFormat)
    {
        var formatLabel = GetFormatLabel(targetFormat);
        var formatRules = GetFormatSpecificRules(targetFormat);

        return $@"You are an AI assistant for an AI-powered CMS, specialized in creating email templates. Generate a new {formatLabel} email template that matches the style of the provided sample.

SAMPLE EMAIL TEMPLATE (format: {formatLabel}):
Name: {sampleTemplate.Name}
Subject: {sampleTemplate.Subject}
From Name: {sampleTemplate.FromName}
Body Template:
{sampleTemplate.BodyTemplate}

CRITICAL RULES - READ CAREFULLY:
1. MATCH VISUAL STYLE: Generate an email template that matches the visual style, colors, fonts, and spacing of the sample
2. OUTPUT MUST BE {formatLabel}: The bodyTemplate must be valid {formatLabel}
3. NO HALLUCINATION: Do not add components or structures that are not needed
4. REUSE PATTERNS: Adapt the layout patterns from the sample

{formatRules}

GENERAL:
- Use web-safe fonts: Arial, Helvetica, Times New Roman, Georgia, Verdana, sans-serif

LIQUID TEMPLATING SYNTAX (use inside text/attribute nodes as needed):
- Variables:     {{{{ variableName }}}}                  e.g. {{{{ firstName }}}}, {{{{ unsubscribeUrl }}}}
- Conditionals: {{% if condition %}}...{{% endif %}}     e.g. {{% if isVip %}}VIP content{{% endif %}}
                {{% unless condition %}}...{{% endunless %}}   e.g. {{% unless unsubscribed %}}footer{{% endunless %}}
- Loops:        {{% for item in items %}}...{{% endfor %}}     e.g. {{% for product in products %}}{{{{ product.name }}}}{{% endfor %}}
- Convert any legacy placeholders (<%token%>, ${{token}}, HTML-encoded) to {{{{ token }}}} Liquid syntax

OUTPUT FORMAT - Return ONLY valid JSON with this exact structure:
{{
  ""name"": ""Template_Name"",
  ""subject"": ""Email Subject Line"",
  ""bodyTemplate"": ""<the template body in {formatLabel} format>"",
  ""fromName"": ""Sender Name"",
  ""format"": ""{formatLabel}""
}}

IMPORTANT: The 'name' field is used as a localisation key and must NEVER be translated. Keep the original template name exactly as-is.
The bodyTemplate must be valid {formatLabel}.";
    }

    private static string BuildUserPrompt(string prompt, string language, Dictionary<string, string>? templateVariables)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Create an email template in {language} language based on this request:");
        sb.AppendLine();
        sb.AppendLine(prompt);

        sb.Append(BuildTemplateVariablesSection(templateVariables));

        sb.AppendLine();
        sb.AppendLine("IMPORTANT REMINDERS:");
        sb.AppendLine("- Match the visual style (colors, fonts, spacing) of the sample template");
        sb.AppendLine("- Use {{ variableName }} Liquid syntax for all variable placeholders");
        sb.AppendLine("- Use {% if condition %}...{% endif %} for conditional blocks");
        sb.AppendLine("- Return only the JSON structure as specified in the system prompt");

        return sb.ToString();
    }

    private static string BuildTemplateVariablesSection(Dictionary<string, string>? templateVariables)
    {
        if (templateVariables == null || templateVariables.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("REQUIRED TEMPLATE VARIABLES \u2014 you MUST include ALL of the following as {{ variableName }} Liquid placeholders in the generated template:");
        foreach (var variable in templateVariables)
        {
            sb.AppendLine($"- {{{{ {variable.Key} }}}}: {variable.Value}");
        }

        return sb.ToString();
    }

    private static EmailTemplateTranslationMetadata ParseGeneratedEmailTemplate(string jsonText)
    {
        try
        {
            // Strip markdown code fences if present
            var cleaned = jsonText.Trim();
            if (cleaned.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    cleaned = cleaned[(firstNewline + 1)..];
                }

                if (cleaned.EndsWith("```", StringComparison.Ordinal))
                {
                    cleaned = cleaned[..^3].TrimEnd();
                }
            }

            using var document = JsonDocument.Parse(cleaned);
            var template = JsonHelper.Deserialize<EmailTemplateTranslationMetadata>(cleaned);

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

    private static string StripMarkdownCodeFences(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = cleaned.IndexOf('\n');
            if (firstNewline >= 0)
            {
                cleaned = cleaned[(firstNewline + 1)..];
            }

            if (cleaned.EndsWith("```", StringComparison.Ordinal))
            {
                cleaned = cleaned[..^3].TrimEnd();
            }
        }

        return cleaned;
    }

    private async Task<EmailTemplate?> FindSampleEmailTemplateAsync(int emailGroupId, string language, int? referenceEmailTemplateId = null)
    {
        if (referenceEmailTemplateId.HasValue)
        {
            var referencedTemplate = await dbContext.EmailTemplates!
                .FirstOrDefaultAsync(et => et.Id == referenceEmailTemplateId.Value);

            if (referencedTemplate == null)
            {
                throw new AIProviderException(
                    "EmailTemplateGeneration",
                    $"Reference email template with ID {referenceEmailTemplateId.Value} was not found.");
            }

            return referencedTemplate;
        }

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