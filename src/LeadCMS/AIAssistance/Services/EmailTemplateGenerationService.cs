// <copyright file="EmailTemplateGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Collections.Frozen;
using System.Reflection;
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
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using LeadCMS.Services;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Core.AIAssistance.Services;

public class EmailTemplateGenerationService : IEmailTemplateGenerationService
{
    private const string DefaultFromEmailSettingKey = "ApiSettings.DefaultFromEmail";
    private const string DefaultFromEmailConfigurationPath = "ApiSettings:DefaultFromEmail";

    // ── Template parameter knowledge ────────────────────────────────────

    /// <summary>
    /// Property names that are internal implementation details and should not be
    /// exposed as template variables.
    /// </summary>
    private static readonly HashSet<string> InternalPropertyNames = new(StringComparer.Ordinal)
    {
        "Data",
        "TestOrder",
        "ContactIp",
        "AccountStatus",
        "HttpCheck",
        "DnsCheck",
        "MxCheck",
        "Free",
        "Disposable",
        "CatchAll",
    };

    /// <summary>
    /// Knowledge block describing all built-in Liquid template parameters available
    /// to email templates. Generated dynamically from <see cref="EmailTemplateService.BuildDummyContact"/>
    /// so it stays in sync with entity changes automatically.
    /// Must be declared after <see cref="InternalPropertyNames"/> to ensure correct static initialisation order.
    /// </summary>
    private static readonly string TemplateParametersKnowledge = BuildTemplateParametersKnowledge();

    // ── Instance fields ─────────────────────────────────────────────────

    private readonly PgDbContext dbContext;
    private readonly ITextGenerationService textGenerationService;
    private readonly IMapper mapper;
    private readonly IHttpContextHelper httpContextHelper;
    private readonly ISettingService settingService;

    public EmailTemplateGenerationService(
        PgDbContext dbContext,
        ITextGenerationService textGenerationService,
        IMapper mapper,
        IHttpContextHelper httpContextHelper,
        ISettingService settingService)
    {
        this.dbContext = dbContext;
        this.textGenerationService = textGenerationService;
        this.mapper = mapper;
        this.httpContextHelper = httpContextHelper;
        this.settingService = settingService;
    }

    // ════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════════════════════════════

    public async Task<EmailTemplateDetailsDto> GenerateEmailTemplateAsync(EmailTemplateGenerationRequest request)
    {
        Log.Information("Starting email template generation for group {EmailGroupId} in language {Language}", request.EmailGroupId, request.Language);

        var emailGroup = await dbContext.EmailGroups!
            .FirstOrDefaultAsync(eg => eg.Id == request.EmailGroupId);

        if (emailGroup == null)
        {
            throw new AIProviderException("EmailTemplateGeneration", $"Email group with ID {request.EmailGroupId} not found");
        }

        var targetCategory = request.Category ?? EmailTemplateCategory.General;

        // Find a sample: explicit reference > database match
        var sampleBody = await ResolveSampleBodyAsync(
            request.ReferenceEmailTemplateId,
            request.EmailGroupId,
            request.Language);

        var (fallbackFromName, fallbackFromEmail) = await ResolveFallbackSenderAsync();

        var systemPrompt = BuildGenerateSystemPrompt(targetCategory, sampleBody, fallbackFromName, fallbackFromEmail);
        var userPrompt = BuildGenerateUserPrompt(request.Prompt, request.Language, request.TemplateVariables, targetCategory, sampleBody != null);

        try
        {
            var response = await textGenerationService.GenerateTextAsync(new TextGenerationRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
            });

            var generated = ParseGeneratedTemplate(response.GeneratedText);

            var entity = new EmailTemplate
            {
                Name = generated.Name,
                Subject = generated.Subject,
                BodyTemplate = generated.BodyTemplate,
                Category = targetCategory,
                FromName = !string.IsNullOrWhiteSpace(generated.FromName) ? generated.FromName : fallbackFromName,
                FromEmail = fallbackFromEmail,
                Language = request.Language,
                EmailGroupId = request.EmailGroupId,
                TranslationKey = null,
                Source = $"AI Generated - Model: {response.Model}, Tokens: {response.TokensUsed}",
            };

            var result = mapper.Map<EmailTemplateDetailsDto>(entity);
            Log.Information("Successfully generated email template for group {EmailGroupId} in language {Language}", request.EmailGroupId, request.Language);
            return result;
        }
        catch (AIProviderException)
        {
            throw;
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

        var currentCategory = request.Category ?? EmailTemplateCategory.General;

        var currentTemplate = new EmailTemplateTranslationMetadata
        {
            Name = request.Name ?? string.Empty,
            Subject = request.Subject ?? string.Empty,
            BodyTemplate = request.BodyTemplate ?? string.Empty,
            FromName = request.FromName ?? string.Empty,
        };

        var currentTemplateJson = JsonHelper.Serialize(currentTemplate);

        // Optional: load reference template as visual guide
        string? referenceBody = null;
        if (request.ReferenceEmailTemplateId.HasValue)
        {
            var refTemplate = await dbContext.EmailTemplates!
                .FirstOrDefaultAsync(et => et.Id == request.ReferenceEmailTemplateId.Value)
                ?? throw new AIProviderException("EmailTemplateEditing", $"Reference email template with ID {request.ReferenceEmailTemplateId.Value} was not found.");

            referenceBody = refTemplate.BodyTemplate;
        }

        var (senderFromName, senderFromEmail) = await ResolveFallbackSenderAsync();

        var systemPrompt = BuildEditSystemPrompt(currentCategory, senderFromName, senderFromEmail);
        var userPrompt = BuildEditUserPrompt(currentTemplateJson, request.Prompt, request.TemplateVariables, currentCategory, referenceBody);

        try
        {
            var response = await textGenerationService.GenerateTextAsync(new TextGenerationRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
            });

            var edited = ParseGeneratedTemplate(response.GeneratedText);

            var entity = new EmailTemplate
            {
                Name = edited.Name,
                Subject = edited.Subject,
                BodyTemplate = edited.BodyTemplate,
                Category = currentCategory,
                FromName = edited.FromName,
                FromEmail = request.FromEmail ?? string.Empty,
                Language = request.Language ?? string.Empty,
                TranslationKey = request.TranslationKey,
                EmailGroupId = request.EmailGroupId ?? 0,
                Source = $"AI Edited - Model: {response.Model}, Tokens: {response.TokensUsed}",
            };

            var result = mapper.Map<EmailTemplateDetailsDto>(entity);
            Log.Information("Successfully edited email template");
            return result;
        }
        catch (AIProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to edit email template");
            throw new AIProviderException("EmailTemplateEditing", "Failed to edit email template", ex);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  PROMPT BUILDING — GENERATE
    // ════════════════════════════════════════════════════════════════════

    private static string BuildGenerateSystemPrompt(
        EmailTemplateCategory category,
        string? sampleBody,
        string senderName,
        string senderEmail)
    {
        var sb = new StringBuilder(8192);

        sb.AppendLine($"You are an AI assistant specialised in creating email templates for a CMS platform.");
        sb.AppendLine($"Generate a new HTML email template.");
        sb.AppendLine();

        // ── Format rules ────────────────────────────────────────────────
        AppendFormatRules(sb);

        // ── Category guidance ───────────────────────────────────────────
        AppendCategoryGuidance(sb, category);

        // ── Sample reference ────────────────────────────────────────────
        if (sampleBody != null)
        {
            sb.AppendLine();
            sb.AppendLine("SAMPLE TEMPLATE — match its visual style, layout, and structural patterns:");
            sb.AppendLine(sampleBody);
            sb.AppendLine("--- END SAMPLE ---");
        }

        // ── Liquid syntax & template parameters ─────────────────────────
        AppendLiquidSyntax(sb);
        sb.Append(TemplateParametersKnowledge);

        // ── Sender signature ────────────────────────────────────────────
        AppendSenderSignatureRules(sb, senderName, senderEmail);

        // ── Output format ───────────────────────────────────────────────
        AppendOutputFormat(sb, "HTML");

        return sb.ToString();
    }

    private static string BuildGenerateUserPrompt(
        string prompt,
        string language,
        Dictionary<string, string>? templateVariables,
        EmailTemplateCategory category,
        bool hasSample)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Create an email template in {language} language based on this request:");
        sb.AppendLine();
        sb.AppendLine(prompt);

        AppendTemplateVariablesSection(sb, templateVariables);

        if (category != EmailTemplateCategory.General)
        {
            sb.AppendLine();
            sb.AppendLine($"EMAIL CATEGORY: This template belongs to the \"{category}\" category. Ensure tone, layout, and content patterns are appropriate for this category.");
        }

        sb.AppendLine();
        sb.AppendLine("IMPORTANT REMINDERS:");
        if (hasSample)
        {
            sb.AppendLine("- Match the visual style (colors, fonts, spacing) of the sample template");
        }

        sb.AppendLine("- Use {{ variableName }} Liquid syntax for all variable placeholders");
        sb.AppendLine("- Use {% if condition %}...{% endif %} for conditional blocks");
        sb.AppendLine("- Return only the JSON structure as specified in the system prompt");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════
    //  PROMPT BUILDING — EDIT
    // ════════════════════════════════════════════════════════════════════

    private static string BuildEditSystemPrompt(
        EmailTemplateCategory category,
        string senderName,
        string senderEmail)
    {
        var sb = new StringBuilder(8192);

        sb.AppendLine($"You are an email template editor assistant for an AI-powered CMS.");
        sb.AppendLine($"Edit the provided HTML email template based on the user's request.");
        sb.AppendLine();
        sb.AppendLine("EDITING RULES:");
        sb.AppendLine("1. PRESERVE STRUCTURE: keep the same logical structure and layout as the original");
        sb.AppendLine("2. CONSERVATIVE EDITS: when ambiguous, make the minimum changes necessary");
        sb.AppendLine("3. NO HALLUCINATION: only use variables listed in AVAILABLE TEMPLATE PARAMETERS or provided via REQUIRED TEMPLATE VARIABLES");
        sb.AppendLine();

        AppendFormatRules(sb);
        AppendCategoryGuidance(sb, category);
        AppendLiquidSyntax(sb);
        sb.Append(TemplateParametersKnowledge);
        AppendSenderSignatureRules(sb, senderName, senderEmail);
        AppendOutputFormat(sb, "HTML");

        sb.AppendLine();
        sb.AppendLine("IMPORTANT: The 'name' field is a localisation key — NEVER translate it. Keep it exactly as-is.");

        return sb.ToString();
    }

    private static string BuildEditUserPrompt(
        string currentTemplateJson,
        string prompt,
        Dictionary<string, string>? templateVariables,
        EmailTemplateCategory category,
        string? referenceBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Current email template:");
        sb.AppendLine(currentTemplateJson);
        sb.AppendLine();
        sb.AppendLine($"User's editing request: {prompt}");

        AppendTemplateVariablesSection(sb, templateVariables);

        if (category != EmailTemplateCategory.General)
        {
            sb.AppendLine();
            sb.AppendLine($"EMAIL CATEGORY: \"{category}\" — ensure edits respect this category's conventions.");
        }

        if (referenceBody != null)
        {
            sb.AppendLine();
            sb.AppendLine("REFERENCE SAMPLE (use as visual / structural guide):");
            sb.AppendLine(referenceBody);
        }

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════
    //  PROMPT BUILDING — SHARED SECTIONS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Appends HTML format rules to the prompt for email template generation.
    /// </summary>
    private static void AppendFormatRules(StringBuilder sb)
    {
        sb.AppendLine("FORMAT RULES (HTML):");
        sb.AppendLine("1. The bodyTemplate MUST be standard, well-formed HTML suitable for email clients");
        sb.AppendLine("2. Use table-based layouts for maximum cross-client compatibility");
        sb.AppendLine("3. Use inline CSS styles for reliable rendering");
        sb.AppendLine("4. Set colors, fonts, spacing via inline style attributes");
    }

    private static void AppendCategoryGuidance(StringBuilder sb, EmailTemplateCategory category)
    {
        var guidance = category switch
        {
            EmailTemplateCategory.PlainText =>
                """

                CATEGORY — PLAIN-TEXT / PERSONAL-STYLE:
                - Minimal formatting — the email should look like it was typed by a real person
                - No hero images, banners, or heavy styling; plain white background
                - Conversational, 1:1 human tone (first person, direct address)
                - Short paragraphs (2-3 sentences) with natural line breaks
                - Simple text-based signature (name, title) — no graphical footers
                - A single inline link is sufficient — avoid styled buttons
                - Ideal for sales outreach, personal follow-ups, relationship-building
                """,

            EmailTemplateCategory.SimpleProfessional =>
                """

                CATEGORY — SIMPLE PROFESSIONAL:
                - Clean, minimal layout: logo/header at top, concise body, subtle footer
                - 1-2 short sections with clear hierarchy (heading → body → CTA)
                - Single, understated CTA button
                - Neutral, professional tones and colour palettes
                - Suitable for SaaS updates, feature announcements, account notifications
                """,

            EmailTemplateCategory.Newsletter =>
                """

                CATEGORY — NEWSLETTER / EDITORIAL:
                - Multi-section layout with clear visual separators
                - Each block: heading, short excerpt, optional image, 'Read more' link
                - Balance text and imagery for a magazine-like feel
                - Include social sharing links and consistent section styling
                """,

            EmailTemplateCategory.Promotional =>
                """

                CATEGORY — PROMOTIONAL / MARKETING:
                - Lead with a strong hero image or banner
                - Discount/offer front and centre
                - Bold, prominent CTA buttons ('Shop Now', 'Claim Offer')
                - Urgency elements (limited time, scarcity)
                - Concise copy — let visuals and CTAs drive action
                """,

            EmailTemplateCategory.Transactional =>
                """

                CATEGORY — TRANSACTIONAL:
                - Clarity and information density over visual flair
                - Structured, scannable tables for order/transaction details
                - Reference numbers, dates, amounts, status prominently displayed
                - Minimal branding — logo and footer are sufficient
                - Include next-step instructions or support contact info
                """,

            EmailTemplateCategory.Lifecycle =>
                """

                CATEGORY — LIFECYCLE / DRIP:
                - Multi-step educational sequence with progressive CTAs
                - Warm, personal greeting using contact's first name
                - Single clear goal and one primary CTA per email
                - Numbered steps, checklists, or progress indicators
                - Clean and inviting design — avoid information overload
                """,

            EmailTemplateCategory.Digest =>
                """

                CATEGORY — DIGEST / REPORT:
                - Data-centric layout: tables, KPI cards, summary metrics
                - Clear sections with descriptive headings
                - Minimal narrative text — let the numbers speak
                - CTA to view full report or dashboard
                """,

            EmailTemplateCategory.Event =>
                """

                CATEGORY — EVENT / INVITATION:
                - Event name, date, time, and location prominent at top
                - Hero image or banner related to the event
                - Clear RSVP or registration CTA button
                - Agenda highlights or speaker cards if applicable
                - Venue/logistics details or virtual meeting link
                """,

            EmailTemplateCategory.Alert =>
                """

                CATEGORY — ALERT / NOTIFICATION:
                - Compact, scannable layout — get to the point immediately
                - Clear summary of what happened and when
                - Colour cues or icons for priority/severity
                - Direct action link or CTA for required response
                - Minimal design — no heavy imagery or promotional elements
                """,

            _ => string.Empty,
        };

        if (!string.IsNullOrEmpty(guidance))
        {
            sb.Append(guidance);
        }
    }

    private static void AppendLiquidSyntax(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("LIQUID TEMPLATING SYNTAX:");
        sb.AppendLine("- Variables:     {{ variableName }}");
        sb.AppendLine("- Conditionals:  {% if condition %}...{% endif %}");
        sb.AppendLine("                 {% unless condition %}...{% endunless %}");
        sb.AppendLine("- Loops:         {% for item in items %}...{% endfor %}");
        sb.AppendLine("- Convert any legacy placeholders (<%token%>, ${token}) to {{ token }} Liquid syntax");
    }

    private static void AppendSenderSignatureRules(StringBuilder sb, string senderName, string senderEmail)
    {
        sb.AppendLine();
        sb.AppendLine("SENDER SIGNATURE RULES — CRITICAL:");
        sb.AppendLine("All template variables ({{ Email }}, {{ Phone }}, {{ FirstName }}, etc.) are the RECIPIENT's data.");
        sb.AppendLine("NEVER use template variables in the sender signature / sign-off section.");
        sb.AppendLine("Instead, hardcode the sender's actual details.");

        if (!string.IsNullOrWhiteSpace(senderName) || !string.IsNullOrWhiteSpace(senderEmail))
        {
            sb.AppendLine();
            sb.AppendLine("Sender details for the signature:");
            if (!string.IsNullOrWhiteSpace(senderName))
            {
                sb.AppendLine($"  Sender Name: {senderName}");
            }

            if (!string.IsNullOrWhiteSpace(senderEmail))
            {
                sb.AppendLine($"  Sender Email: {senderEmail}");
            }
        }
    }

    private static void AppendOutputFormat(StringBuilder sb, string formatLabel)
    {
        sb.AppendLine();
        sb.AppendLine("OUTPUT FORMAT — return ONLY valid JSON with this exact structure:");
        sb.AppendLine("{");
        sb.AppendLine("  \"name\": \"Template_Name\",");
        sb.AppendLine("  \"subject\": \"Email Subject Line\",");
        sb.AppendLine($"  \"bodyTemplate\": \"<the template body in {formatLabel} format>\",");
        sb.AppendLine("  \"fromName\": \"Sender Name\"");
        sb.AppendLine("}");
    }

    private static void AppendTemplateVariablesSection(StringBuilder sb, Dictionary<string, string>? templateVariables)
    {
        if (templateVariables == null || templateVariables.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("REQUIRED TEMPLATE VARIABLES — you MUST include ALL of the following as {{ variableName }} Liquid placeholders:");
        foreach (var variable in templateVariables)
        {
            sb.AppendLine($"- {{{{ {variable.Key} }}}}: {variable.Value}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  TEMPLATE PARAMETERS KNOWLEDGE
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds the template parameters knowledge section dynamically from a full dummy contact
    /// so the AI prompt stays in sync with entity changes automatically.
    /// </summary>
    private static string BuildTemplateParametersKnowledge()
    {
        var contact = EmailTemplateService.BuildDummyContact(PreviewContactType.Full);
        var args = TemplateArgumentsBuilder.FromContact(contact);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("AVAILABLE TEMPLATE PARAMETERS (Liquid variables injected at send time):");
        sb.AppendLine();

        // Scalar fields
        sb.AppendLine("Scalar fields (use as {{ FieldName }}):");
        foreach (var kvp in args)
        {
            if (kvp.Value is not string strVal)
            {
                continue;
            }

            sb.Append("  {{ ").Append(kvp.Key).Append(" }}");
            if (!string.IsNullOrEmpty(strVal))
            {
                sb.Append(" — e.g. \"").Append(strVal).Append('"');
            }

            sb.AppendLine();
        }

        // Nested objects
        sb.AppendLine();
        sb.AppendLine("Nested Account object — {{ Account.PropertyName }}:");
        AppendEntityProperties(sb, typeof(Account), contact.Account, "Account", indent: 2);

        sb.AppendLine();
        sb.AppendLine("Nested Domain object — {{ Domain.PropertyName }}:");
        AppendEntityProperties(sb, typeof(Domain), contact.Domain, "Domain", indent: 2);

        // Orders collection
        var sampleOrder = contact.Orders?.FirstOrDefault();
        sb.AppendLine();
        sb.AppendLine("Orders collection — {% for order in Orders %}...{% endfor %}:");
        sb.AppendLine("  Each Order has:");
        AppendEntityProperties(sb, typeof(Order), sampleOrder, "order", indent: 4);

        var sampleItem = sampleOrder?.OrderItems?.FirstOrDefault();
        sb.AppendLine();
        sb.AppendLine("  Order → OrderItems — {% for item in order.OrderItems %}...{% endfor %}:");
        sb.AppendLine("    Each OrderItem has:");
        AppendEntityProperties(sb, typeof(OrderItem), sampleItem, "item", indent: 6);

        sb.AppendLine();
        sb.AppendLine("  Order → Discounts — {% for discount in order.Discounts %}...{% endfor %}:");
        sb.AppendLine("    Each Discount has:");
        AppendEntityProperties(sb, typeof(Discount), instance: null, "discount", indent: 6);

        // Deals collection
        var sampleDeal = contact.Deals?.FirstOrDefault();
        sb.AppendLine();
        sb.AppendLine("Deals collection — {% for deal in Deals %}...{% endfor %}:");
        sb.AppendLine("  Each Deal has:");
        AppendEntityProperties(sb, typeof(Deal), sampleDeal, "deal", indent: 4);

        if (sampleDeal?.DealPipeline != null)
        {
            sb.AppendLine("    deal.DealPipeline — nested object:");
            AppendEntityProperties(sb, typeof(DealPipeline), sampleDeal.DealPipeline, "deal.DealPipeline", indent: 6);
        }

        if (sampleDeal?.DealPipelineStage != null)
        {
            sb.AppendLine("    deal.DealPipelineStage — nested object:");
            AppendEntityProperties(sb, typeof(DealPipelineStage), sampleDeal.DealPipelineStage, "deal.DealPipelineStage", indent: 6);
        }

        // Usage examples
        sb.AppendLine();
        sb.AppendLine("Usage examples:");
        sb.AppendLine("  {{ FirstName }}");
        sb.AppendLine("  {{ Account.Name }}");
        sb.AppendLine("  {% for order in Orders %}");
        sb.AppendLine("    #{{ order.RefNo }} — {{ order.Total }} {{ order.Currency }}");
        sb.AppendLine("    {% for item in order.OrderItems %}");
        sb.AppendLine("      {{ item.ProductName }} × {{ item.Quantity }}");
        sb.AppendLine("    {% endfor %}");
        sb.AppendLine("  {% endfor %}");

        sb.AppendLine();
        sb.AppendLine("Custom variables — callers may pass additional key-value pairs through TemplateVariables.");
        sb.AppendLine();
        sb.AppendLine("STRICT VARIABLE RULE — DO NOT HALLUCINATE VARIABLES:");
        sb.AppendLine("Only use template variables listed above or explicitly provided through TemplateVariables.");
        sb.AppendLine("Using a non-existent variable causes a rendering failure.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT — ALL VARIABLES ABOVE ARE RECIPIENT DATA:");
        sb.AppendLine("Every variable listed above belongs to the EMAIL RECIPIENT.");
        sb.AppendLine("NEVER use these variables in the sender signature or sign-off section.");
        sb.AppendLine("The sender's identity is provided separately — see SENDER SIGNATURE RULES.");

        return sb.ToString();
    }

    /// <summary>
    /// Appends the template-relevant properties of an entity type to the prompt builder.
    /// </summary>
    private static void AppendEntityProperties(
        StringBuilder sb,
        Type entityType,
        object? instance,
        string prefix,
        int indent)
    {
        var padding = new string(' ', indent);
        var properties = entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead
                && IsTemplateRelevantType(p.PropertyType)
                && !IsIdOrForeignKey(p)
                && !InternalPropertyNames.Contains(p.Name))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            object? value = null;
            if (instance != null)
            {
                try
                {
                    value = prop.GetValue(instance);
                }
                catch
                {
                    // Ignore reflection errors on sample instance.
                }
            }

            var valueStr = value?.ToString();
            sb.Append(padding).Append("{{ ").Append(prefix).Append('.').Append(prop.Name).Append(" }}");
            if (!string.IsNullOrEmpty(valueStr))
            {
                sb.Append(" — e.g. \"").Append(valueStr).Append('"');
            }

            sb.AppendLine();
        }
    }

    private static bool IsTemplateRelevantType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(string)
            || underlying == typeof(int)
            || underlying == typeof(long)
            || underlying == typeof(decimal)
            || underlying == typeof(double)
            || underlying == typeof(float)
            || underlying == typeof(bool)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying.IsEnum;
    }

    private static bool IsIdOrForeignKey(PropertyInfo prop)
    {
        if (string.Equals(prop.Name, "Id", StringComparison.Ordinal))
        {
            return true;
        }

        return prop.Name.Length > 2 && prop.Name.EndsWith("Id", StringComparison.Ordinal);
    }

    // ════════════════════════════════════════════════════════════════════
    //  PARSING & UTILITIES
    // ════════════════════════════════════════════════════════════════════

    private static EmailTemplateTranslationMetadata ParseGeneratedTemplate(string jsonText)
    {
        try
        {
            var cleaned = StripMarkdownCodeFences(jsonText);

            using var document = JsonDocument.Parse(cleaned);
            var template = JsonHelper.Deserialize<EmailTemplateTranslationMetadata>(cleaned)
                ?? throw new InvalidOperationException("Failed to deserialize generated email template JSON");

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

    // ════════════════════════════════════════════════════════════════════
    //  PRIVATE INSTANCE HELPERS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves the best available sample body to use as a reference in the AI prompt.
    /// Priority: explicit reference template ID → database template in same group.
    /// </summary>
    private async Task<string?> ResolveSampleBodyAsync(
        int? referenceTemplateId,
        int emailGroupId,
        string language)
    {
        // 1. Explicit reference
        if (referenceTemplateId.HasValue)
        {
            var refTemplate = await dbContext.EmailTemplates!
                .FirstOrDefaultAsync(et => et.Id == referenceTemplateId.Value)
                ?? throw new AIProviderException("EmailTemplateGeneration", $"Reference email template with ID {referenceTemplateId.Value} was not found.");

            return refTemplate.BodyTemplate;
        }

        // 2. Database template in same group
        var dbSample = await dbContext.EmailTemplates!
            .Where(et => et.EmailGroupId == emailGroupId)
            .OrderByDescending(et => et.Language == language)
            .FirstOrDefaultAsync();

        return dbSample?.BodyTemplate;
    }

    private async Task<(string fromName, string fromEmail)> ResolveFallbackSenderAsync()
    {
        var currentUser = await httpContextHelper.GetCurrentUserAsync();
        var currentUserId = currentUser?.Id;

        var fallbackFromEmail = await settingService.GetSettingWithFallbackAsync(
            DefaultFromEmailSettingKey,
            DefaultFromEmailConfigurationPath,
            currentUserId) ?? string.Empty;

        var fallbackFromName = currentUser?.DisplayName;
        if (string.IsNullOrWhiteSpace(fallbackFromName))
        {
            fallbackFromName = currentUser?.UserName;
        }

        if (string.IsNullOrWhiteSpace(fallbackFromName))
        {
            fallbackFromName = fallbackFromEmail;
        }

        return (fallbackFromName ?? string.Empty, fallbackFromEmail);
    }
}
