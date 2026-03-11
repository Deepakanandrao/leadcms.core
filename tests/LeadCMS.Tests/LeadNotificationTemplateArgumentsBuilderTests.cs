// <copyright file="LeadNotificationTemplateArgumentsBuilderTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using LeadCMS.Plugin.Site.DTOs;

namespace LeadCMS.Tests;

public class LeadNotificationInfoTemplateArgumentsTests
{
    [Fact]
    public void ToTemplateArguments_IncludesTimezoneFields()
    {
        var leadInfo = new LeadNotificationInfo
        {
            Email = "lead@example.com",
            FirstName = "Ada",
            LastName = "Lovelace",
            Timezone = -300,
            IpAddressV4 = "198.51.100.10",
        };

        var args = leadInfo.ToTemplateArguments();

        args.Comparer.Should().Be(StringComparer.OrdinalIgnoreCase);
        args.Should().ContainKey("Timezone").WhoseValue.Should().Be("-300");
        args.Should().ContainKey("TimezoneFormatted").WhoseValue.Should().Be("UTC-5");
        args.Should().ContainKey("IpAddress").WhoseValue.Should().Be("198.51.100.10");
        args.Should().ContainKey("FullName").WhoseValue.Should().Be("Ada Lovelace");
        args["timezoneformatted"].Should().Be("UTC-5");
    }

    [Fact]
    public void ToTemplateArguments_WithoutOptionalStringValues_DoesNotAddEmptyStringArguments()
    {
        var leadInfo = new LeadNotificationInfo();

        var args = leadInfo.ToTemplateArguments();

        args.Should().NotContainKey("Email");
        args.Should().NotContainKey("FullName");
        args.Should().NotContainKey("Subject");
        args.Should().NotContainKey("Message");
        args.Should().NotContainKey("Title");
        args.Should().NotContainKey("PageUrl");
        args.Should().NotContainKey("UserAgent");
        args.Should().NotContainKey("Timezone");
        args.Should().NotContainKey("TimezoneFormatted");
        args.Should().NotContainKey("ContactId");
    }

    [Fact]
    public void ToTemplateArguments_MergedOnTopOfContact_SubmittedDataWins()
    {
        var contact = new Contact
        {
            Email = "old@example.com",
            FirstName = "Old",
            CompanyName = "Old Corp",
            Timezone = 60,
        };

        var leadInfo = new LeadNotificationInfo
        {
            Email = "new@example.com",
            FirstName = "New",
            CompanyName = "New Corp",
            Timezone = -300,
        };

        var args = TemplateArgumentsBuilder.FromContact(contact, includeNestedObjects: false);
        TemplateArgumentsBuilder.Merge(args, leadInfo.ToTemplateArguments());

        // Submitted data wins
        args["Email"].Should().Be("new@example.com");
        args["FirstName"].Should().Be("New");
        args["CompanyName"].Should().Be("New Corp");
        args["Timezone"].Should().Be("-300");
        args["TimezoneFormatted"].Should().Be("UTC-5");
    }
}
