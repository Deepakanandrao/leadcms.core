// <copyright file="TemplateArgumentsBuilderTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

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
}
