// <copyright file="ContactMetadataUpdateHelperTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Models;
using LeadCMS.Plugin.Site.Services;

namespace LeadCMS.Tests;

public class ContactMetadataUpdateHelperTests
{
    [Fact]
    public void ApplyMetadata_OverwritesLocaleAndMergesTagsWithDedup()
    {
        var contact = new Contact
        {
            Language = "en",
            Timezone = 60,
            Tags = new[] { "Existing", "Shared" },
        };

        ContactMetadataUpdateHelper.ApplyMetadata(contact, "fr", 180, new[] { "shared", "New" });

        contact.Language.Should().Be("fr");
        contact.Timezone.Should().Be(180);
        contact.Tags.Should().BeEquivalentTo("Existing", "Shared", "New");
        contact.PendingUpdates.Should().BeNull();
    }

    [Fact]
    public void ApplyFirstTouchUtm_SetsUtmOnNewContact()
    {
        var contact = new Contact();
        var utm = new Utms { Source = "google", Medium = "cpc", Campaign = "spring" };

        ContactMetadataUpdateHelper.ApplyFirstTouchUtm(contact, utm);

        contact.Utms.Should().NotBeNull();
        contact.Utms!.Source.Should().Be("google");
        contact.Utms.Medium.Should().Be("cpc");
        contact.Utms.Campaign.Should().Be("spring");
    }

    [Fact]
    public void ApplyFirstTouchUtm_DoesNotOverwriteExistingUtm()
    {
        var contact = new Contact
        {
            Utms = new Utms { Source = "newsletter", Campaign = "launch" },
        };

        var newUtm = new Utms { Source = "google", Campaign = "remarketing" };

        ContactMetadataUpdateHelper.ApplyFirstTouchUtm(contact, newUtm);

        contact.Utms!.Source.Should().Be("newsletter");
        contact.Utms.Campaign.Should().Be("launch");
    }

    [Fact]
    public void ApplyFirstTouchUtm_IgnoresNullOrEmptyUtm()
    {
        var contact = new Contact();

        ContactMetadataUpdateHelper.ApplyFirstTouchUtm(contact, null);
        contact.Utms.Should().BeNull();

        ContactMetadataUpdateHelper.ApplyFirstTouchUtm(contact, new Utms());
        contact.Utms.Should().BeNull();
    }
}