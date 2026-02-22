// <copyright file="EmailTemplateRenderingTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

/// <summary>
/// Integration tests that verify the full email template rendering pipeline:
///   1. Create email template via API (various formats and placeholder styles).
///   2. Trigger send through IEmailFromTemplateService.
///   3. Assert on the rendered HTML stored in EmailLog.
/// </summary>
public class EmailTemplateRenderingTests : BaseTestAutoLogin
{
    private static readonly string EmailGroupsApi = "/api/email-groups";
    private static readonly string EmailTemplatesApi = "/api/email-templates";

    public EmailTemplateRenderingTests()
    {
        TrackEntityType<EmailGroup>();
        TrackEntityType<EmailTemplate>();
        TrackEntityType<EmailLog>();
    }

    // ────────────────────────────────────────────────────────
    //  HTML FORMAT — variable rendering
    // ────────────────────────────────────────────────────────

    [Fact]
    public async Task HtmlTemplate_WithLiquidVariables_ShouldRenderVariables()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "liquid_html",
            "Hello {{ firstName }}",
            "<p>Hi {{ firstName }}, your order {{ orderNumber }} is ready.</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["firstName"] = "Alice",
            ["orderNumber"] = "ORD-42",
        };

        await SendEmailAsync("liquid_html", "en", "recipient@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Hello Alice");
        log.HtmlBody.Should().Contain("Hi Alice, your order ORD-42 is ready.");
        log.HtmlBody.Should().NotContain("{{");
    }

    [Fact]
    public async Task HtmlTemplate_WithLegacyAngleBracketPlaceholders_ShouldRenderVariables()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "legacy_angle",
            "Order <%orderNumber%>",
            "<p>Dear <%firstName%>, amount: <%amount%></p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["firstName"] = "Bob",
            ["orderNumber"] = "X-100",
            ["amount"] = "$19.99",
        };

        await SendEmailAsync("legacy_angle", "en", "bob@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Order X-100");
        log.HtmlBody.Should().Contain("Dear Bob, amount: $19.99");
        log.HtmlBody.Should().NotContain("<%");
    }

    [Fact]
    public async Task HtmlTemplate_WithLegacyDollarBracePlaceholders_ShouldRenderVariables()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "legacy_dollar",
            "Hi ${firstName}",
            "<div>${firstName} — your code is ${code}.</div>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["firstName"] = "Charlie",
            ["code"] = "ABC123",
        };

        await SendEmailAsync("legacy_dollar", "en", "charlie@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Hi Charlie");
        log.HtmlBody.Should().Contain("Charlie — your code is ABC123.");
        log.HtmlBody.Should().NotContain("${");
    }

    [Fact]
    public async Task HtmlTemplate_WithHtmlEncodedLegacyPlaceholders_ShouldRenderVariables()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "legacy_encoded",
            "Welcome",
            "<p>Hello &lt;%userName%&gt;, verify at &lt;%verifyUrl%&gt;</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["userName"] = "Dana",
            ["verifyUrl"] = "https://example.com/verify",
        };

        await SendEmailAsync("legacy_encoded", "en", "dana@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("Hello Dana, verify at https://example.com/verify");
        log.HtmlBody.Should().NotContain("&lt;%");
    }

    [Fact]
    public async Task HtmlTemplate_WithMixedPlaceholderFormats_ShouldRenderAllVariables()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "mixed_html",
            "{{ greeting }}",
            "<p>{{ greeting }} <%firstName%>! Code: ${code}, link: &lt;%link%&gt;</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["greeting"] = "Welcome",
            ["firstName"] = "Eve",
            ["code"] = "Z99",
            ["link"] = "https://app.example.com",
        };

        await SendEmailAsync("mixed_html", "en", "eve@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Welcome");
        log.HtmlBody.Should().Contain("Welcome Eve! Code: Z99, link: https://app.example.com");
    }

    // ────────────────────────────────────────────────────────
    //  MJML FORMAT — compilation + variable rendering
    // ────────────────────────────────────────────────────────

    [Fact]
    public async Task MjmlTemplate_WithLiquidVariables_ShouldCompileAndRenderVariables()
    {
        var groupId = await CreateEmailGroupAsync();
        var mjml = BuildMjmlBody(
            "<mj-text>Hello {{ firstName }}, your order {{ orderNumber }} is confirmed.</mj-text>");

        await CreateTemplateViaApiAsync(
            groupId,
            "liquid_mjml",
            "Order {{ orderNumber }}",
            mjml,
            EmailTemplateFormat.Mjml);

        var variables = new Dictionary<string, object>
        {
            ["firstName"] = "Frank",
            ["orderNumber"] = "MJ-001",
        };

        await SendEmailAsync("liquid_mjml", "en", "frank@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Order MJ-001");
        log.HtmlBody.Should().Contain("Hello Frank, your order MJ-001 is confirmed.");
        log.HtmlBody.Should().NotContain("<mjml>");
        log.HtmlBody.Should().NotContain("<mj-text>");
        log.HtmlBody.Should().Contain("<html");
    }

    [Fact]
    public async Task MjmlTemplate_WithLegacyPlaceholders_ShouldCompileAndRenderVariables()
    {
        var groupId = await CreateEmailGroupAsync();
        var mjml = BuildMjmlBody(
            "<mj-text>Hi <%name%>, your code is ${code}.</mj-text>");

        await CreateTemplateViaApiAsync(
            groupId,
            "legacy_mjml",
            "Code ${code}",
            mjml,
            EmailTemplateFormat.Mjml);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Grace",
            ["code"] = "PROMO50",
        };

        await SendEmailAsync("legacy_mjml", "en", "grace@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Code PROMO50");
        log.HtmlBody.Should().Contain("Hi Grace, your code is PROMO50.");
        log.HtmlBody.Should().NotContain("<mjml>");
        log.HtmlBody.Should().NotContain("<%");
        log.HtmlBody.Should().NotContain("${");
    }

    [Fact]
    public async Task MjmlTemplate_WithMixedPlaceholders_ShouldCompileAndRenderAll()
    {
        var groupId = await CreateEmailGroupAsync();
        var mjml = BuildMjmlBody(
            "<mj-text>{{ greeting }} <%firstName%>!</mj-text>" +
            "<mj-text>Link: ${link}</mj-text>");

        await CreateTemplateViaApiAsync(
            groupId,
            "mixed_mjml",
            "{{ greeting }}",
            mjml,
            EmailTemplateFormat.Mjml);

        var variables = new Dictionary<string, object>
        {
            ["greeting"] = "Hi there",
            ["firstName"] = "Hank",
            ["link"] = "https://example.com/dashboard",
        };

        await SendEmailAsync("mixed_mjml", "en", "hank@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Hi there");
        log.HtmlBody.Should().Contain("Hi there Hank!");
        log.HtmlBody.Should().Contain("Link: https://example.com/dashboard");
        log.HtmlBody.Should().NotContain("<mjml>");
    }

    // ────────────────────────────────────────────────────────
    //  LIQUID CONDITIONALS — if / unless
    // ────────────────────────────────────────────────────────

    [Fact]
    public async Task HtmlTemplate_WithLiquidIfBlock_WhenConditionTrue_ShouldRenderBlock()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "if_true",
            "Status",
            "<p>Hello {{ name }}.{% if isVip %} You are a VIP member!{% endif %}</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Ivy",
            ["isVip"] = "true",
        };

        await SendEmailAsync("if_true", "en", "ivy@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("Hello Ivy. You are a VIP member!");
    }

    [Fact]
    public async Task HtmlTemplate_WithLiquidIfBlock_WhenConditionMissing_ShouldOmitBlock()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "if_false",
            "Status",
            "<p>Hello {{ name }}.{% if isVip %} You are a VIP member!{% endif %}</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Jake",
        };

        await SendEmailAsync("if_false", "en", "jake@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("Hello Jake.");
        log.HtmlBody.Should().NotContain("VIP member");
    }

    [Fact]
    public async Task HtmlTemplate_WithLiquidUnless_WhenConditionMissing_ShouldRenderBlock()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "unless_test",
            "Info",
            "<p>{% unless hideBanner %}SPECIAL OFFER!{% endunless %} Hi {{ name }}.</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Kate",
        };

        await SendEmailAsync("unless_test", "en", "kate@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("SPECIAL OFFER!");
        log.HtmlBody.Should().Contain("Hi Kate.");
    }

    [Fact]
    public async Task HtmlTemplate_WithLiquidUnless_WhenConditionProvided_ShouldOmitBlock()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "unless_true",
            "Info",
            "<p>{% unless hideBanner %}SPECIAL OFFER!{% endunless %} Hi {{ name }}.</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Leo",
            ["hideBanner"] = "yes",
        };

        await SendEmailAsync("unless_true", "en", "leo@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().NotContain("SPECIAL OFFER!");
        log.HtmlBody.Should().Contain("Hi Leo.");
    }

    [Fact]
    public async Task MjmlTemplate_WithConditionals_ShouldCompileAndEvaluateConditions()
    {
        var groupId = await CreateEmailGroupAsync();
        var mjml = BuildMjmlBody(
            "<mj-text>Hello {{ name }}.{% if showPromo %} Use promo: {{ promoCode }}!{% endif %}</mj-text>");

        await CreateTemplateViaApiAsync(
            groupId,
            "mjml_cond",
            "Promo",
            mjml,
            EmailTemplateFormat.Mjml);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Mia",
            ["showPromo"] = "true",
            ["promoCode"] = "SAVE20",
        };

        await SendEmailAsync("mjml_cond", "en", "mia@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().NotContain("<mjml>");
        log.HtmlBody.Should().Contain("Hello Mia. Use promo: SAVE20!");
    }

    // ────────────────────────────────────────────────────────
    //  EDGE CASES and QA-style break-it scenarios
    // ────────────────────────────────────────────────────────

    [Fact]
    public async Task Template_WithNoVariablesProvided_ShouldRenderEmptyForMissingVars()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "no_vars",
            "Hello {{ name }}",
            "<p>Your code is {{ code }}.</p>",
            EmailTemplateFormat.Html);

        await SendEmailAsync("no_vars", "en", "nobody@test.net", null);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Hello ");
        log.HtmlBody.Should().Contain("Your code is .");
    }

    [Fact]
    public async Task Template_WithPlainTextBody_NoVariables_ShouldSendAsIs()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "plain_body",
            "Subject only",
            "<p>No variables here, just static text.</p>",
            EmailTemplateFormat.Html);

        await SendEmailAsync("plain_body", "en", "test@test.net", null);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Subject only");
        log.HtmlBody.Should().Contain("No variables here, just static text.");
    }

    [Fact]
    public async Task Template_WithSpecialCharactersInVariables_ShouldRenderCorrectly()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "special_chars",
            "Hi {{ name }}",
            "<p>Note: {{ note }}</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "O'Brien & Partners",
            ["note"] = "Amount: $100 — 50% off!",
        };

        await SendEmailAsync("special_chars", "en", "obrien@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("50% off!");
    }

    [Fact]
    public async Task MjmlTemplate_ShouldProduceResponsiveHtmlStructure()
    {
        var groupId = await CreateEmailGroupAsync();
        var mjml = @"<mjml>
  <mj-head>
    <mj-attributes>
      <mj-all font-family=""Arial, sans-serif"" />
    </mj-attributes>
  </mj-head>
  <mj-body background-color=""#f4f4f4"">
    <mj-section background-color=""#ffffff"">
      <mj-column>
        <mj-text font-size=""20px"" color=""#333333"">Welcome {{ name }}!</mj-text>
        <mj-button href=""{{ actionUrl }}"" background-color=""#007bff"">Click Here</mj-button>
      </mj-column>
    </mj-section>
  </mj-body>
</mjml>";

        await CreateTemplateViaApiAsync(
            groupId,
            "mjml_responsive",
            "Welcome {{ name }}",
            mjml,
            EmailTemplateFormat.Mjml);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Nora",
            ["actionUrl"] = "https://example.com/action",
        };

        await SendEmailAsync("mjml_responsive", "en", "nora@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Welcome Nora");
        log.HtmlBody.Should().NotContain("<mjml>");
        log.HtmlBody.Should().NotContain("<mj-section>");
        log.HtmlBody.Should().NotContain("<mj-column>");
        log.HtmlBody.Should().NotContain("<mj-text>");
        log.HtmlBody.Should().NotContain("<mj-button>");
        log.HtmlBody.Should().Contain("<table");
        log.HtmlBody.Should().Contain("Welcome Nora!");
        log.HtmlBody.Should().Contain("https://example.com/action");
    }

    [Fact]
    public async Task HtmlTemplate_FormatFieldDefaultsToHtml_WhenNotSpecified()
    {
        var groupId = await CreateEmailGroupAsync();

        var dto = new EmailTemplateCreateDto
        {
            Name = "default_format",
            Subject = "Test",
            BodyTemplate = "<p>{{ message }}</p>",
            FromEmail = "test@test.net",
            FromName = "Test",
            Language = "en",
            EmailGroupId = groupId,
        };

        var created = await PostTest<EmailTemplateDetailsDto>(EmailTemplatesApi, dto);
        created.Should().NotBeNull();
        created!.Format.Should().Be(EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object> { ["message"] = "It works!" };

        await SendEmailAsync("default_format", "en", "test@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("<p>It works!</p>");
    }

    [Fact]
    public async Task HtmlTemplate_ShouldNotGoThroughMjmlCompiler()
    {
        var groupId = await CreateEmailGroupAsync();
        var htmlBody = "<html><body><table><tr><td>Row 1 {{ val }}</td></tr></table></body></html>";

        await CreateTemplateViaApiAsync(
            groupId,
            "html_passthrough",
            "Test",
            htmlBody,
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object> { ["val"] = "DATA" };

        await SendEmailAsync("html_passthrough", "en", "test@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("<html><body><table><tr><td>Row 1 DATA</td></tr></table></body></html>");
    }

    [Fact]
    public async Task MjmlTemplate_WithLiquidVariablesInAttributes_ShouldRender()
    {
        var groupId = await CreateEmailGroupAsync();
        var mjml = BuildMjmlBody(
            "<mj-text>Click below</mj-text>" +
            @"<mj-button href=""{{ link }}"">Go</mj-button>");

        await CreateTemplateViaApiAsync(
            groupId,
            "mjml_attr_var",
            "Link",
            mjml,
            EmailTemplateFormat.Mjml);

        var variables = new Dictionary<string, object>
        {
            ["link"] = "https://example.com/go",
        };

        await SendEmailAsync("mjml_attr_var", "en", "user@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("https://example.com/go");
        log.HtmlBody.Should().NotContain("{{");
    }

    // ────────────────────────────────────────────────────────
    //  Line-break preservation in variable values
    // ────────────────────────────────────────────────────────

    [Fact]
    public async Task HtmlTemplate_WithNewlinesInVariableValue_ShouldConvertToBrTags()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "html_newlines",
            "Address for {{ name }}",
            "<p>{{ address }}</p>",
            EmailTemplateFormat.Html);

        var variables = new Dictionary<string, object>
        {
            ["name"] = "Alice",
            ["address"] = "123 Main St\nApt 4B\nNew York, NY 10001",
        };

        await SendEmailAsync("html_newlines", "en", "addr@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Address for Alice");
        log.HtmlBody.Should().Contain("123 Main St<br />Apt 4B<br />New York, NY 10001");
        log.HtmlBody.Should().NotContain("{{");
    }

    [Fact]
    public async Task MjmlTemplate_WithNewlinesInVariableValue_ShouldConvertToBrTags()
    {
        var groupId = await CreateEmailGroupAsync();
        var mjml = BuildMjmlBody("<mj-text>{{ details }}</mj-text>");

        await CreateTemplateViaApiAsync(
            groupId,
            "mjml_newlines",
            "Info",
            mjml,
            EmailTemplateFormat.Mjml);

        var variables = new Dictionary<string, object>
        {
            ["details"] = "Line one\nLine two\nLine three",
        };

        await SendEmailAsync("mjml_newlines", "en", "nl@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("Line one<br />Line two<br />Line three");
        log.HtmlBody.Should().NotContain("{{");
    }

    // ────────────────────────────────────────────────────────
    //  Helpers — static first (SA1204)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal valid MJML document wrapping the given inner components.
    /// </summary>
    private static string BuildMjmlBody(string innerComponents)
    {
        return $"<mjml><mj-body><mj-section><mj-column>{innerComponents}</mj-column></mj-section></mj-body></mjml>";
    }

    private async Task<int> CreateEmailGroupAsync()
    {
        var group = new TestEmailGroup(Guid.NewGuid().ToString("N")[..8]);
        var url = await PostTest(EmailGroupsApi, group);
        var created = await GetTest<EmailGroupDetailsDto>(url);
        created.Should().NotBeNull();
        return created!.Id;
    }

    private async Task CreateTemplateViaApiAsync(
        int groupId,
        string name,
        string subject,
        string body,
        EmailTemplateFormat format)
    {
        var dto = new EmailTemplateCreateDto
        {
            Name = name,
            Subject = subject,
            BodyTemplate = body,
            Format = format,
            FromEmail = "sender@test.net",
            FromName = "Test Sender",
            Language = "en",
            EmailGroupId = groupId,
        };

        var created = await PostTest<EmailTemplateDetailsDto>(EmailTemplatesApi, dto);
        created.Should().NotBeNull();
        created!.Name.Should().Be(name);
        created.Format.Should().Be(format);
    }

    private async Task SendEmailAsync(
        string templateName,
        string language,
        string recipient,
        Dictionary<string, object>? variables)
    {
        using var scope = App.Services.CreateScope();
        var emailFromTemplateService = scope.ServiceProvider.GetRequiredService<IEmailFromTemplateService>();
        await emailFromTemplateService.SendAsync(
            templateName,
            language,
            new[] { recipient },
            variables,
            null);
    }

    private async Task<EmailLog> GetLastEmailLogAsync()
    {
        var dbContext = App.GetDbContext()!;
        var log = await dbContext.EmailLogs!
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        log.Should().NotBeNull("An email log entry should have been created after sending");
        return log!;
    }
}
