// <copyright file="EmailTemplateRenderingTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Constants;
using LeadCMS.Tests.TestServices;
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
        TrackEntityType<Media>();
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
            "<p>Hi {{ firstName }}, your order {{ orderNumber }} is ready.</p>");

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
            "<p>Dear <%firstName%>, amount: <%amount%></p>");

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
            "<div>${firstName} — your code is ${code}.</div>");

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
            "<p>Hello &lt;%userName%&gt;, verify at &lt;%verifyUrl%&gt;</p>");

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
            "<p>{{ greeting }} <%firstName%>! Code: ${code}, link: &lt;%link%&gt;</p>");

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
            "<p>Hello {{ name }}.{% if isVip %} You are a VIP member!{% endif %}</p>");

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
            "<p>Hello {{ name }}.{% if isVip %} You are a VIP member!{% endif %}</p>");

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
            "<p>{% unless hideBanner %}SPECIAL OFFER!{% endunless %} Hi {{ name }}.</p>");

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
            "<p>{% unless hideBanner %}SPECIAL OFFER!{% endunless %} Hi {{ name }}.</p>");

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
            "<p>Your code is {{ code }}.</p>");

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
            "<p>No variables here, just static text.</p>");

        await SendEmailAsync("plain_body", "en", "test@test.net", null);

        var log = await GetLastEmailLogAsync();
        log.Subject.Should().Be("Subject only");
        log.HtmlBody.Should().Contain("No variables here, just static text.");
    }

    [Fact]
    public async Task HtmlTemplate_WithSiteLinkVariables_UsesGeneralSystemSettings()
    {
        var siteUrl = "https://example.org";
        var unsubscribeUrl = "https://example.org/unsubscribe";
        var privacyUrl = "https://example.org/privacy";

        await Request(HttpMethod.Put, $"/api/settings/system/{Uri.EscapeDataString(SettingKeys.GeneralSiteUrl)}?value={Uri.EscapeDataString(siteUrl)}", null);
        await Request(HttpMethod.Put, $"/api/settings/system/{Uri.EscapeDataString(SettingKeys.GeneralUnsubscribeUrl)}?value={Uri.EscapeDataString(unsubscribeUrl)}", null);
        await Request(HttpMethod.Put, $"/api/settings/system/{Uri.EscapeDataString(SettingKeys.GeneralPrivacyUrl)}?value={Uri.EscapeDataString(privacyUrl)}", null);

        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "site_links_general",
            "Links",
            "<p>{{ site_url }}</p><p>{{ unsubscribe_url }}</p><p>{{ privacy_url }}</p>");

        await SendEmailAsync("site_links_general", "en", "recipient@test.net", null);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain(siteUrl);
        log.HtmlBody.Should().Contain(unsubscribeUrl);
        log.HtmlBody.Should().Contain(privacyUrl);
    }

    [Fact]
    public async Task HtmlTemplate_WithDerivedSiteLinkVariables_UsesGeneralSiteUrlWhenOtherLinksEmpty()
    {
        var siteUrl = "https://derived.example.org";

        await Request(HttpMethod.Put, $"/api/settings/system/{Uri.EscapeDataString(SettingKeys.GeneralSiteUrl)}?value={Uri.EscapeDataString(siteUrl)}", null);
        await Request(HttpMethod.Put, $"/api/settings/system/{Uri.EscapeDataString(SettingKeys.GeneralUnsubscribeUrl)}?value=", null);
        await Request(HttpMethod.Put, $"/api/settings/system/{Uri.EscapeDataString(SettingKeys.GeneralPrivacyUrl)}?value=", null);

        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "site_links_derived",
            "Links",
            "<p>{{ site_url }}</p><p>{{ unsubscribe_url }}</p><p>{{ privacy_url }}</p>");

        await SendEmailAsync("site_links_derived", "en", "recipient@test.net", null);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain(siteUrl);
        log.HtmlBody.Should().Contain($"{siteUrl}/unsubscribe");
        log.HtmlBody.Should().Contain($"{siteUrl}/privacy");
    }

    [Fact]
    public async Task Template_WithSpecialCharactersInVariables_ShouldRenderCorrectly()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "special_chars",
            "Hi {{ name }}",
            "<p>Note: {{ note }}</p>");

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
    public async Task HtmlTemplate_CreatedWithMinimalFields_ShouldRenderCorrectly()
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

        var variables = new Dictionary<string, object> { ["message"] = "It works!" };

        await SendEmailAsync("default_format", "en", "test@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("<p>It works!</p>");
    }

    [Fact]
    public async Task HtmlTemplate_ShouldRenderHtmlDirectly()
    {
        var groupId = await CreateEmailGroupAsync();
        var htmlBody = "<html><body><table><tr><td>Row 1 {{ val }}</td></tr></table></body></html>";

        await CreateTemplateViaApiAsync(
            groupId,
            "html_passthrough",
            "Test",
            htmlBody);

        var variables = new Dictionary<string, object> { ["val"] = "DATA" };

        await SendEmailAsync("html_passthrough", "en", "test@test.net", variables);

        var log = await GetLastEmailLogAsync();
        log.HtmlBody.Should().Contain("<html><body><table><tr><td>Row 1 DATA</td></tr></table></body></html>");
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
            "<p>{{ address }}</p>");

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

    // ────────────────────────────────────────────────────────
    //  Template media attachments
    // ────────────────────────────────────────────────────────

    [Fact]
    public async Task Template_WithStaticAttachment_ShouldResolveMediaAndAttach()
    {
        var groupId = await CreateEmailGroupAsync();
        var mediaData = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF magic bytes
        await InsertMediaAsync("invoices", "receipt.pdf", "application/pdf", mediaData);

        await CreateTemplateWithAttachmentsAsync(
            groupId,
            "static_attach",
            "Your receipt",
            "<p>See attached.</p>",
            new[] { "invoices/receipt.pdf" });

        var (_, attachments) = await SendEmailAndCaptureAsync("static_attach", "en", "user@test.net", null);

        attachments.Should().NotBeNull();
        attachments.Should().HaveCount(1);
        attachments![0].FileName.Should().Be("receipt.pdf");
        attachments[0].File.Should().BeEquivalentTo(mediaData);
    }

    [Fact]
    public async Task Template_WithLiquidAttachmentPath_ShouldRenderPathThenResolveMedia()
    {
        var groupId = await CreateEmailGroupAsync();
        var mediaData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        await InsertMediaAsync("reports", "q1-summary.pdf", "application/pdf", mediaData);

        await CreateTemplateWithAttachmentsAsync(
            groupId,
            "liquid_attach",
            "Report for {{ quarter }}",
            "<p>Report attached.</p>",
            new[] { "reports/{{ fileName }}" });

        var variables = new Dictionary<string, object>
        {
            ["quarter"] = "Q1",
            ["fileName"] = "q1-summary.pdf",
        };

        var (_, attachments) = await SendEmailAndCaptureAsync("liquid_attach", "en", "mgr@test.net", variables);

        attachments.Should().NotBeNull();
        attachments.Should().HaveCount(1);
        attachments![0].FileName.Should().Be("q1-summary.pdf");
        attachments[0].File.Should().BeEquivalentTo(mediaData);
    }

    [Fact]
    public async Task Template_WithMultipleAttachments_ShouldResolveAll()
    {
        var groupId = await CreateEmailGroupAsync();
        var pdfData = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var imgData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        await InsertMediaAsync("docs", "terms.pdf", "application/pdf", pdfData);
        await InsertMediaAsync("brand", "logo.png", "image/png", imgData);

        await CreateTemplateWithAttachmentsAsync(
            groupId,
            "multi_attach",
            "Welcome",
            "<p>Welcome aboard!</p>",
            new[] { "docs/terms.pdf", "brand/logo.png" });

        var (_, attachments) = await SendEmailAndCaptureAsync("multi_attach", "en", "new@test.net", null);

        attachments.Should().NotBeNull();
        attachments.Should().HaveCount(2);
        attachments!.Select(a => a.FileName).Should().BeEquivalentTo("terms.pdf", "logo.png");
    }

    [Fact]
    public async Task Template_WithMissingMedia_ShouldSkipAndSendWithoutAttachment()
    {
        var groupId = await CreateEmailGroupAsync();

        await CreateTemplateWithAttachmentsAsync(
            groupId,
            "missing_attach",
            "Info",
            "<p>Some info.</p>",
            new[] { "nonexistent/file.pdf" });

        var (log, attachments) = await SendEmailAndCaptureAsync("missing_attach", "en", "test@test.net", null);

        // Email should still be sent successfully
        log.Should().NotBeNull();
        attachments.Should().BeNull();
    }

    [Fact]
    public async Task Template_WithNoAttachments_ShouldSendNormally()
    {
        var groupId = await CreateEmailGroupAsync();
        await CreateTemplateViaApiAsync(
            groupId,
            "no_attach",
            "Plain email",
            "<p>No attachments here.</p>");

        var (log, attachments) = await SendEmailAndCaptureAsync("no_attach", "en", "plain@test.net", null);

        log.Should().NotBeNull();
        attachments.Should().BeNull();
    }

    [Fact]
    public async Task Template_WithAttachments_ShouldPersistViaApi()
    {
        var groupId = await CreateEmailGroupAsync();

        var dto = new EmailTemplateCreateDto
        {
            Name = "persist_attach",
            Subject = "Test",
            BodyTemplate = "<p>Test</p>",
            FromEmail = "sender@test.net",
            FromName = "Sender",
            Language = "en",
            EmailGroupId = groupId,
            Attachments = new[] { "scope1/file1.pdf", "scope2/{{ dynamic }}" },
        };

        var created = await PostTest<EmailTemplateDetailsDto>(EmailTemplatesApi, dto);
        created.Should().NotBeNull();
        created!.Attachments.Should().BeEquivalentTo("scope1/file1.pdf", "scope2/{{ dynamic }}");

        // Verify GET returns the same attachments
        var fetched = await GetTest<EmailTemplateDetailsDto>($"{EmailTemplatesApi}/{created.Id}");
        fetched.Should().NotBeNull();
        fetched!.Attachments.Should().BeEquivalentTo("scope1/file1.pdf", "scope2/{{ dynamic }}");
    }

    [Fact]
    public async Task Template_WithAttachments_ShouldUpdateViaApi()
    {
        var groupId = await CreateEmailGroupAsync();

        var dto = new EmailTemplateCreateDto
        {
            Name = "update_attach",
            Subject = "Test",
            BodyTemplate = "<p>Test</p>",
            FromEmail = "sender@test.net",
            FromName = "Sender",
            Language = "en",
            EmailGroupId = groupId,
            Attachments = new[] { "scope1/file1.pdf" },
        };

        var created = await PostTest<EmailTemplateDetailsDto>(EmailTemplatesApi, dto);
        created.Should().NotBeNull();

        var updateDto = new EmailTemplateUpdateDto
        {
            Attachments = new[] { "scope2/file2.png", "scope3/file3.docx" },
        };

        await PatchTest($"{EmailTemplatesApi}/{created!.Id}", updateDto);

        var updated = await GetTest<EmailTemplateDetailsDto>($"{EmailTemplatesApi}/{created.Id}");
        updated.Should().NotBeNull();
        updated!.Attachments.Should().BeEquivalentTo("scope2/file2.png", "scope3/file3.docx");
    }

    // ────────────────────────────────────────────────────────
    //  Helpers — static first (SA1204)
    // ────────────────────────────────────────────────────────

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
        string body)
    {
        await CreateTemplateWithAttachmentsAsync(groupId, name, subject, body, Array.Empty<string>());
    }

    private async Task CreateTemplateWithAttachmentsAsync(
        int groupId,
        string name,
        string subject,
        string body,
        string[] attachments)
    {
        var dto = new EmailTemplateCreateDto
        {
            Name = name,
            Subject = subject,
            BodyTemplate = body,
            FromEmail = "sender@test.net",
            FromName = "Test Sender",
            Language = "en",
            EmailGroupId = groupId,
            Attachments = attachments,
        };

        var created = await PostTest<EmailTemplateDetailsDto>(EmailTemplatesApi, dto);
        created.Should().NotBeNull();
        created!.Name.Should().Be(name);
    }

    private async Task InsertMediaAsync(string scopeUid, string name, string mimeType, byte[] data)
    {
        var dbContext = App.GetDbContext()!;
        dbContext.Media!.Add(new Media
        {
            ScopeUid = scopeUid,
            Name = name,
            MimeType = mimeType,
            Extension = Path.GetExtension(name),
            Data = data,
            Size = data.Length,
        });
        await dbContext.SaveChangesAsync();
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

    private async Task<(EmailLog log, List<AttachmentDto>? attachments)> SendEmailAndCaptureAsync(
        string templateName,
        string language,
        string recipient,
        Dictionary<string, object>? variables)
    {
        using var scope = App.Services.CreateScope();
        var emailFromTemplateService = scope.ServiceProvider.GetRequiredService<IEmailFromTemplateService>();
        var testEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>() as TestEmailService;

        await emailFromTemplateService.SendAsync(
            templateName,
            language,
            new[] { recipient },
            variables,
            null);

        var log = await GetLastEmailLogAsync();
        return (log, testEmailService?.LastSentAttachments);
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
