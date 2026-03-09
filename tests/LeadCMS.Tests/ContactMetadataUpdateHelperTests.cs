// <copyright file="ContactMetadataUpdateHelperTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

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
}