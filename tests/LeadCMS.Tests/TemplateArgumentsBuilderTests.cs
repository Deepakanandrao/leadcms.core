// <copyright file="TemplateArgumentsBuilderTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Models;

namespace LeadCMS.Tests;

public class TemplateArgumentsBuilderTests
{
    [Fact]
    public void FromContact_WithUpdatedByIp_PrefersUpdatedIpAddress()
    {
        var contact = new Contact
        {
            Email = "test@example.com",
            CreatedByIp = "198.51.100.10",
            UpdatedByIp = "203.0.113.20",
        };

        var args = TemplateArgumentsBuilder.FromContact(contact);

        args.Should().ContainKey("IpAddress").WhoseValue.Should().Be("203.0.113.20");
    }

    [Fact]
    public void FromContact_WithUpdatedByUserAgent_AddsPreferredUserDeviceSummary()
    {
        var contact = new Contact
        {
            Email = "test@example.com",
            CreatedByUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            UpdatedByUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
        };

        var args = TemplateArgumentsBuilder.FromContact(contact);

        args.Should().ContainKey("UserDeviceSummary");
        args["UserDeviceSummary"].Should().BeOfType<string>();
        args["UserDeviceSummary"].ToString().Should().Contain("Chrome 122");
        args["UserDeviceSummary"].ToString().Should().Contain("Mac");
    }

    [Fact]
    public void FromContact_WithoutUpdatedByIp_FallsBackToCreatedIpAddress()
    {
        var contact = new Contact
        {
            Email = "test@example.com",
            CreatedByIp = "198.51.100.10",
        };

        var args = TemplateArgumentsBuilder.FromContact(contact);

        args.Should().ContainKey("IpAddress").WhoseValue.Should().Be("198.51.100.10");
    }

    [Fact]
    public void FromContact_WithoutOptionalStringValues_DoesNotAddEmptyStringArguments()
    {
        var contact = new Contact();

        var args = TemplateArgumentsBuilder.FromContact(contact, includeNestedObjects: false);

        args.Should().NotContainKey("Email");
        args.Should().NotContainKey("FirstName");
        args.Should().NotContainKey("FullName");
        args.Should().NotContainKey("Timezone");
        args.Should().NotContainKey("TimezoneFormatted");
        args.Should().NotContainKey("IpAddress");
        args.Should().ContainKey("DealsCount").WhoseValue.Should().Be(0);
        args.Should().ContainKey("OrdersCount").WhoseValue.Should().Be(0);
    }

    [Fact]
    public void FromContact_WithUtmParameters_AddsContactUtmKeys()
    {
        var contact = new Contact
        {
            Email = "test@example.com",
            Utms = new Utms
            {
                Source = "google",
                Medium = "cpc",
                Campaign = "spring_sale",
            },
        };

        var args = TemplateArgumentsBuilder.FromContact(contact, includeNestedObjects: false);

        args.Should().ContainKey("contact_utm_source").WhoseValue.Should().Be("google");
        args.Should().ContainKey("contact_utm_medium").WhoseValue.Should().Be("cpc");
        args.Should().ContainKey("contact_utm_campaign").WhoseValue.Should().Be("spring_sale");
        args.Should().NotContainKey("contact_utm_content");
        args.Should().NotContainKey("contact_utm_term");
    }

    [Fact]
    public void FromContact_WithoutUtmParameters_DoesNotAddContactUtmKeys()
    {
        var contact = new Contact { Email = "test@example.com" };

        var args = TemplateArgumentsBuilder.FromContact(contact, includeNestedObjects: false);

        args.Should().NotContainKey("contact_utm_source");
        args.Should().NotContainKey("contact_utm_medium");
        args.Should().NotContainKey("contact_utm_campaign");
    }

    [Fact]
    public void WithPendingUpdates_OverlaysPendingValueOntoExistingArgs()
    {
        var contact = new Contact
        {
            FirstName = "Alice",
            PendingUpdates = new List<PendingContactUpdate>
            {
                new() { Field = nameof(Contact.FirstName), ProposedValue = "Bob" },
            },
        };

        var args = new Dictionary<string, object> { ["FirstName"] = "Alice" };

        TemplateArgumentsBuilder.WithPendingUpdates(args, contact);

        args["FirstName"].Should().Be("Bob");
    }

    [Fact]
    public void WithPendingUpdates_RebuildFullNameWhenNamePartPending()
    {
        var contact = new Contact
        {
            FirstName = "Alice",
            LastName = "Smith",
            PendingUpdates = new List<PendingContactUpdate>
            {
                new() { Field = nameof(Contact.FirstName), ProposedValue = "Bob" },
            },
        };

        var args = new Dictionary<string, object>
        {
            ["FirstName"] = "Alice",
            ["LastName"] = "Smith",
            ["FullName"] = "Alice Smith",
        };

        TemplateArgumentsBuilder.WithPendingUpdates(args, contact);

        args["FullName"].Should().Be("Bob Smith");
    }

    [Fact]
    public void WithPendingUpdates_NullContact_ReturnsArgsUnchanged()
    {
        var args = new Dictionary<string, object> { ["FirstName"] = "Alice" };

        var result = TemplateArgumentsBuilder.WithPendingUpdates(args, null);

        result.Should().BeSameAs(args);
        args["FirstName"].Should().Be("Alice");
    }

    [Fact]
    public void WithPendingUpdates_NoPendingUpdates_ReturnsArgsUnchanged()
    {
        var contact = new Contact { FirstName = "Alice" };

        var args = new Dictionary<string, object> { ["FirstName"] = "Alice" };

        TemplateArgumentsBuilder.WithPendingUpdates(args, contact);

        args["FirstName"].Should().Be("Alice");
    }

    [Fact]
    public void WithEmailHistory_BothSentAndReceived_AddsAllParameters()
    {
        var sentLog = new EmailLog
        {
            Subject = "Welcome!",
            FromEmail = "team@example.com",
            FromName = "Team",
            HtmlBody = "<p>Welcome aboard</p>",
            Status = EmailStatus.Sent,
            CreatedAt = new DateTime(2025, 11, 1, 10, 30, 0, DateTimeKind.Utc),
        };

        var receivedLog = new EmailLog
        {
            Subject = "Re: Welcome!",
            FromEmail = "user@example.com",
            FromName = "Jane Doe",
            HtmlBody = "<p>Thanks!</p>",
            Status = EmailStatus.Received,
            CreatedAt = new DateTime(2025, 11, 2, 14, 0, 0, DateTimeKind.Utc),
        };

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        TemplateArgumentsBuilder.WithEmailHistory(args, sentLog, receivedLog);

        args.Should().ContainKey("LastSentEmailDate").WhoseValue.Should().Be("2025-11-01 10:30:00");
        args.Should().ContainKey("LastSentEmailTitle").WhoseValue.Should().Be("Welcome!");
        args.Should().ContainKey("LastSentEmailBody").WhoseValue.Should().Be("<p>Welcome aboard</p>");
        args.Should().ContainKey("LastSentEmailFromName").WhoseValue.Should().Be("Team");
        args.Should().ContainKey("LastSentEmailFromEmail").WhoseValue.Should().Be("team@example.com");

        args.Should().ContainKey("LastReceivedEmailDate").WhoseValue.Should().Be("2025-11-02 14:00:00");
        args.Should().ContainKey("LastReceivedEmailTitle").WhoseValue.Should().Be("Re: Welcome!");
        args.Should().ContainKey("LastReceivedEmailBody").WhoseValue.Should().Be("<p>Thanks!</p>");
        args.Should().ContainKey("LastReceivedEmailFromName").WhoseValue.Should().Be("Jane Doe");
        args.Should().ContainKey("LastReceivedEmailFromEmail").WhoseValue.Should().Be("user@example.com");
    }

    [Fact]
    public void WithEmailHistory_OnlySentEmail_AddsOnlySentParameters()
    {
        var sentLog = new EmailLog
        {
            Subject = "Newsletter",
            FromEmail = "news@example.com",
            FromName = "Newsletter Team",
            HtmlBody = "<p>Latest news</p>",
            Status = EmailStatus.Sent,
            CreatedAt = new DateTime(2025, 10, 15, 9, 0, 0, DateTimeKind.Utc),
        };

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        TemplateArgumentsBuilder.WithEmailHistory(args, sentLog, null);

        args.Should().ContainKey("LastSentEmailTitle").WhoseValue.Should().Be("Newsletter");
        args.Should().ContainKey("LastSentEmailFromEmail").WhoseValue.Should().Be("news@example.com");
        args.Should().NotContainKey("LastReceivedEmailDate");
        args.Should().NotContainKey("LastReceivedEmailTitle");
    }

    [Fact]
    public void WithEmailHistory_NullBoth_ReturnsArgsUnchanged()
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirstName"] = "Alice",
        };

        var result = TemplateArgumentsBuilder.WithEmailHistory(args, null, null);

        result.Should().BeSameAs(args);
        args.Should().HaveCount(1);
        args.Should().ContainKey("FirstName");
    }

    [Fact]
    public void WithEmailHistory_MissingFromName_DoesNotAddFromNameKey()
    {
        var sentLog = new EmailLog
        {
            Subject = "Hello",
            FromEmail = "noreply@example.com",
            HtmlBody = "<p>Hi</p>",
            Status = EmailStatus.Sent,
            CreatedAt = new DateTime(2025, 10, 1, 8, 0, 0, DateTimeKind.Utc),
        };

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        TemplateArgumentsBuilder.WithEmailHistory(args, sentLog, null);

        args.Should().ContainKey("LastSentEmailTitle");
        args.Should().ContainKey("LastSentEmailFromEmail");
        args.Should().NotContainKey("LastSentEmailFromName");
    }
}
