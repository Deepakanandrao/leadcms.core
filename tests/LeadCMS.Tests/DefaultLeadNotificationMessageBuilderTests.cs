// <copyright file="DefaultLeadNotificationMessageBuilderTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.Site.DTOs;
using LeadCMS.Plugin.Site.Services;

namespace LeadCMS.Tests;

public class DefaultLeadNotificationMessageBuilderTests
{
    [Fact]
    public void EnrichTemplateArguments_WithUserAgent_AddsOnlyUserDeviceSummary()
    {
        var builder = new DefaultLeadNotificationMessageBuilder();
        var leadInfo = new LeadNotificationInfo
        {
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
        };

        var args = leadInfo.ToTemplateArguments();
        builder.EnrichTemplateArguments(args, leadInfo);

        args.Should().ContainKey("UserAgent").WhoseValue.Should().Be(leadInfo.UserAgent);
        args.Should().ContainKey("userDeviceSummary");
    }
}
