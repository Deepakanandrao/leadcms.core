// <copyright file="EmailTemplateGenerationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Collections.Frozen;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AutoMapper;
using LeadCMS.Core.AIAssistance.Configuration;
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

        var systemPrompt = await BuildGenerateSystemPromptAsync(targetCategory, sampleBody, fallbackFromName, fallbackFromEmail);
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

        var systemPrompt = await BuildEditSystemPromptAsync(currentCategory, senderFromName, senderFromEmail);
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
    //  PROMPT BUILDING — STATIC HELPERS
    // ════════════════════════════════════════════════════════════════════

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

    private static void AddIfPresent(List<string> items, string label, List<Setting> settings, string key)
    {
        var setting = settings.FirstOrDefault(s => s.Key == key);
        if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
        {
            items.Add($"{label}: {setting.Value.Trim()}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  PROMPT BUILDING — SHARED SECTIONS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Appends HTML format rules to the prompt for email template generation.
    /// These rules encode cross-client email compatibility best practices so that
    /// generated HTML renders correctly in Gmail, Outlook (Word engine), Apple Mail,
    /// Yahoo Mail, and other major clients.
    /// </summary>
    private static void AppendFormatRules(StringBuilder sb)
    {
        sb.AppendLine("FORMAT RULES (HTML — maximum cross-client compatibility):");
        sb.AppendLine();

        // ── Document structure ───────────────────────────────────────
        sb.AppendLine("Document structure:");
        sb.AppendLine("1. Start with <!DOCTYPE html>");
        sb.AppendLine("2. Open <html lang=\"en\" xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\">");
        sb.AppendLine("3. Include <head> with:");
        sb.AppendLine("   <meta charset=\"UTF-8\">");
        sb.AppendLine("   <meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\">");
        sb.AppendLine("   <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("   <title>Email</title>");
        sb.AppendLine("4. <body style=\"margin:0; padding:0;\">");
        sb.AppendLine();

        // ── Layout ──────────────────────────────────────────────────
        sb.AppendLine("Layout:");
        sb.AppendLine("5. Use table-based layout for structure — tables provide the most consistent rendering across email clients");
        sb.AppendLine("6. Main content wrapper table: width 600px, centered with margin:0 auto or align=\"center\"");
        sb.AppendLine("7. Add role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" on every layout table (critical for accessibility)");
        sb.AppendLine("8. Do NOT rely on <div>-based layout, CSS Grid, or Flexbox for core structure");
        sb.AppendLine("9. Prefer single-column layouts — they render reliably on mobile and desktop");
        sb.AppendLine();

        // ── CSS ─────────────────────────────────────────────────────
        sb.AppendLine("CSS:");
        sb.AppendLine("10. Use inline CSS styles on every element (style=\"...\" attribute) — many clients strip <style> blocks");
        sb.AppendLine("11. Avoid CSS shorthand where possible (use font-size, font-family, font-weight separately)");
        sb.AppendLine("12. No JavaScript — it is stripped by all email clients");
        sb.AppendLine("13. No position:fixed or position:absolute for content elements");
        sb.AppendLine("14. No external CSS files or <link> stylesheet references");
        sb.AppendLine();

        // ── Typography ──────────────────────────────────────────────
        sb.AppendLine("Typography:");
        sb.AppendLine("15. Use font stacks with web-safe fallbacks: e.g. font-family: 'Roboto', Arial, Helvetica, sans-serif");
        sb.AppendLine("16. Always set explicit font-size, line-height, and color on text elements");
        sb.AppendLine("17. Web fonts are optional enhancement — the email MUST be readable with fallback fonts only");
        sb.AppendLine();

        // ── Images ──────────────────────────────────────────────────
        sb.AppendLine("Images:");
        sb.AppendLine("18. Always include alt text on images");
        sb.AppendLine("19. Set explicit width and height attributes on <img> tags");
        sb.AppendLine("20. Add style=\"display:block;\" on content images to prevent gaps");
        sb.AppendLine("21. Use absolute HTTPS URLs for image sources in production templates");
        sb.AppendLine("22. Never create image-only emails — always include live text");
        sb.AppendLine();

        // ── Buttons / CTA ───────────────────────────────────────────
        sb.AppendLine("Buttons / CTA:");
        sb.AppendLine("23. Use bulletproof button pattern: a table cell with background-color and a nested <a> link");
        sb.AppendLine("24. Do NOT rely solely on border-radius for button styling (Outlook ignores it)");
        sb.AppendLine();

        // ── Backgrounds & colours ───────────────────────────────────
        sb.AppendLine("Backgrounds and colours:");
        sb.AppendLine("25. Prefer solid background colours");
        sb.AppendLine("26. If using background images, always provide a solid-colour fallback");
        sb.AppendLine("27. Duplicate key background colour as bgcolor HTML attribute on <table>/<td> where appropriate");
        sb.AppendLine();

        // ── Preheader ───────────────────────────────────────────────
        sb.AppendLine("Preheader:");
        sb.AppendLine("28. Include hidden preheader text as the FIRST element inside <body>, before the layout table:");
        sb.AppendLine("    <div style=\"display:none;font-size:1px;line-height:1px;max-height:0px;max-width:0px;opacity:0;overflow:hidden;mso-hide:all;font-family:sans-serif;\">Preheader text here</div>");
        sb.AppendLine();

        // ── Accessibility ───────────────────────────────────────────
        sb.AppendLine("Accessibility:");
        sb.AppendLine("29. Use semantic HTML: <p> for paragraphs, <h1>-<h6> for headings");
        sb.AppendLine("30. Set lang attribute on <html> tag");
        sb.AppendLine("31. Ensure sufficient colour contrast between text and background");
        sb.AppendLine("32. Email must remain understandable with images disabled");
        sb.AppendLine();

        // ── Spacing ─────────────────────────────────────────────────
        sb.AppendLine("Spacing:");
        sb.AppendLine("33. Use inline padding on <td> elements or cellpadding on tables for spacing");
        sb.AppendLine("34. Avoid margin on block elements (inconsistent in email clients) — prefer padding on table cells");
        sb.AppendLine();

        // ── Dark mode ───────────────────────────────────────────────
        sb.AppendLine("Dark mode (progressive enhancement only):");
        sb.AppendLine("35. Dark mode tweaks are allowed ONLY in a <style> block using @media (prefers-color-scheme: dark)");
        sb.AppendLine("36. The baseline email MUST look correct WITHOUT dark-mode styles");
        sb.AppendLine();

        // ── Output hygiene ──────────────────────────────────────────
        sb.AppendLine("Output hygiene:");
        sb.AppendLine("37. Keep markup compact but readable");
        sb.AppendLine("38. Do not remove fallback code or VML conditionals");
        sb.AppendLine("39. Prefer stable, boring HTML over clever HTML");
    }

    private static void AppendCategoryGuidance(StringBuilder sb, EmailTemplateCategory category)
    {
        var guidance = category switch
        {
            EmailTemplateCategory.PlainText =>
                """

                CATEGORY — PLAIN-TEXT / PERSONAL-STYLE:
                Goal: The email MUST look like it was typed by a real person in their email client.

                Tone and content:
                - Conversational, 1:1 human tone (first person, direct address)
                - Short paragraphs (2-3 sentences) with natural line breaks
                - Simple text-based signature (name, title) — no graphical footers
                - Ideal for sales outreach, personal follow-ups, relationship-building

                Layout — STRICT rules:
                - Left-aligned text only — NO center alignment anywhere (no align="center", no margin:0 auto, no text-align:center)
                - Plain white background (#ffffff) — NO coloured backgrounds, gradients, or background images
                - NO hero images, banners, logos, or decorative graphics
                - NO styled CTA buttons — use a plain inline <a> link instead
                - NO large padded containers, cards, or boxed sections
                - NO horizontal rules or decorative separators
                - NO padding on the outer table cell or body — zero padding everywhere (the email must start flush like a real typed email)
                - NO wrapper table with a fixed width — use width="100%" only
                - Minimal inline styles: only font-family, font-size, line-height, and color
                - The email MUST look indistinguishable from a manually typed message in Gmail/Outlook

                Structural sample — follow this exact pattern:
                ```html
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1.0">
                  <title>Email</title>
                </head>
                <body style="margin:0; padding:0; background-color:#ffffff;">
                  <div style="display:none;font-size:1px;line-height:1px;max-height:0px;max-width:0px;opacity:0;overflow:hidden;mso-hide:all;font-family:sans-serif;">Preheader text</div>
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%">
                    <tr>
                      <td style="font-family:Arial, Helvetica, sans-serif; font-size:14px; line-height:1.5; color:#222222;">
                        <p style="margin:0 0 14px 0;">Hi {{ FirstName }},</p>
                        <p style="margin:0 0 14px 0;">Your message body here. Keep it short and personal.</p>
                        <p style="margin:0 0 14px 0;">Here is a link if needed: <a href="https://example.com" style="color:#1a73e8;">Click here</a></p>
                        <p style="margin:0 0 0 0;">Best regards,<br>Sender Name<br>Sender Title</p>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                ```

                DO NOT deviate from this minimal style for PlainText templates.
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
        sb.AppendLine("Orders collection (sorted newest-first by UpdatedAt/CreatedAt) — {% for order in Orders %}...{% endfor %}:");
        sb.AppendLine("  The first item is always the most recent. Use {% for order in Orders limit:1 %} to access only the latest order.");
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
        sb.AppendLine("Deals collection (sorted newest-first by UpdatedAt/CreatedAt) — {% for deal in Deals %}...{% endfor %}:");
        sb.AppendLine("  The first item is always the most recent. Use {% for deal in Deals limit:1 %} to access only the latest deal.");
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
        sb.AppendLine("  Most recent order only:");
        sb.AppendLine("  {% for order in Orders limit:1 %}");
        sb.AppendLine("    Your latest order #{{ order.RefNo }}");
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
    //  PROMPT BUILDING — GENERATE (ASYNC)
    // ════════════════════════════════════════════════════════════════════

    private async Task<string> BuildGenerateSystemPromptAsync(
        EmailTemplateCategory category,
        string? sampleBody,
        string senderName,
        string senderEmail)
    {
        var sb = new StringBuilder(8192);

        sb.AppendLine($"You are an AI assistant specialised in creating email templates for a CMS platform.");
        sb.AppendLine($"Generate a new HTML email template.");
        sb.AppendLine();

        // ── Site profile ────────────────────────────────────────────────
        await AppendSiteProfileSectionAsync(sb);

        // ── Email template instructions ─────────────────────────────────
        await AppendEmailTemplateInstructionsAsync(sb);

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

    // ════════════════════════════════════════════════════════════════════
    //  PROMPT BUILDING — EDIT (ASYNC)
    // ════════════════════════════════════════════════════════════════════

    private async Task<string> BuildEditSystemPromptAsync(
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

        // ── Site profile ────────────────────────────────────────────────
        await AppendSiteProfileSectionAsync(sb);

        // ── Email template instructions ─────────────────────────────────
        await AppendEmailTemplateInstructionsAsync(sb);

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

    // ════════════════════════════════════════════════════════════════════
    //  PROMPT BUILDING — SITE PROFILE & INSTRUCTIONS
    // ════════════════════════════════════════════════════════════════════

    private async Task AppendSiteProfileSectionAsync(StringBuilder sb)
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

        var settings = await settingService.FindSettingsByKeysAsync(keys);

        var items = new List<string>();
        AddIfPresent(items, "Topic", settings, AiSettingKeys.SiteTopic);
        AddIfPresent(items, "Audience", settings, AiSettingKeys.SiteAudience);
        AddIfPresent(items, "Voice", settings, AiSettingKeys.BrandVoice);
        AddIfPresent(items, "Preferred terms", settings, AiSettingKeys.PreferredTerms);
        AddIfPresent(items, "Avoid", settings, AiSettingKeys.AvoidTerms);
        AddIfPresent(items, "Style examples", settings, AiSettingKeys.StyleExamples);

        if (items.Count > 0)
        {
            sb.AppendLine("SITE PROFILE (use this to understand site context and tailor the email template accordingly):");
            foreach (var item in items)
            {
                sb.AppendLine($"- {item}");
            }

            sb.AppendLine();
        }
    }

    private async Task AppendEmailTemplateInstructionsAsync(StringBuilder sb)
    {
        var instructions = await settingService.GetSystemSettingAsync(AiSettingKeys.EmailTemplateInstructions);
        if (!string.IsNullOrWhiteSpace(instructions))
        {
            sb.AppendLine("EMAIL TEMPLATE INSTRUCTIONS (follow these requirements when generating the template):");
            sb.AppendLine(instructions.Trim());
            sb.AppendLine();
        }
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
