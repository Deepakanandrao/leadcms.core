// <copyright file="TemplateArgumentsBuilderTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;

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
}
