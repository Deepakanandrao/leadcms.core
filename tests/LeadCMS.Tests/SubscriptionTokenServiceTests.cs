// <copyright file="SubscriptionTokenServiceTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.Site.Services;

namespace LeadCMS.Tests;

public class SubscriptionTokenServiceTests
{
    [Fact]
    public void GenerateAndValidate_WithTags_RoundTripsTags()
    {
        var service = new SubscriptionTokenService("test-secret");

        var token = service.Generate(
            "subscriber@example.test",
            "SubscriberNewsletters",
            "en",
            120,
            new[] { "Newsletter", "VIP" },
            TimeSpan.FromHours(1));

        var payload = service.Validate(token);

        payload.Should().NotBeNull();
        payload!.Tags.Should().BeEquivalentTo("Newsletter", "VIP");
    }
}
