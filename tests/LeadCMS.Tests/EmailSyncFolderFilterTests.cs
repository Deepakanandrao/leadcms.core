// <copyright file="EmailSyncFolderFilterTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using FluentAssertions;
using LeadCMS.EmailSync.Tasks;
using Xunit;

namespace LeadCMS.Tests;

public class EmailSyncFolderFilterTests
{
    private static readonly string[] DefaultKeywords = EmailSyncTask.DefaultIgnoredFolderKeywords;

    private static readonly string[] DefaultWhitelist = new[]
    {
        "INBOX",
        "Clients/Acme",
        "Projects",
    };

    [Theory]
    [InlineData("INBOX")]
    [InlineData("Work")]
    [InlineData("Clients/Acme")]
    [InlineData("Projects.Active")]
    [InlineData("Sent")]
    [InlineData("Sent Items")]
    [InlineData("[Gmail]/Sent Mail")]
    [InlineData("Outbox")]
    [InlineData("[Gmail]/All Mail")]
    public void IsFolderIgnored_RegularAndSentFolders_ReturnsFalse(string folderName)
    {
        EmailSyncTask.IsFolderIgnored(folderName, DefaultKeywords).Should().BeFalse();
    }

    [Theory]
    [InlineData("Spam")]
    [InlineData("Junk")]
    [InlineData("Trash")]
    [InlineData("Drafts")]
    [InlineData("Draft")]
    [InlineData("Deleted")]
    [InlineData("Archive")]
    [InlineData("Archived")]
    [InlineData("Bin")]
    [InlineData("Clutter")]
    [InlineData("Bulk")]
    [InlineData("Bulk Mail")]
    public void IsFolderIgnored_TopLevelIgnoredFolders_ReturnsTrue(string folderName)
    {
        EmailSyncTask.IsFolderIgnored(folderName, DefaultKeywords).Should().BeTrue();
    }

    [Theory]
    [InlineData("INBOX/Spam")]
    [InlineData("Folders/Trash")]
    [InlineData("Mail/Junk/2024")]
    public void IsFolderIgnored_NestedIgnoredFolders_ReturnsTrue(string folderName)
    {
        EmailSyncTask.IsFolderIgnored(folderName, DefaultKeywords).Should().BeTrue();
    }

    [Theory]
    [InlineData("spam")]
    [InlineData("SPAM")]
    [InlineData("jUNK")]
    [InlineData("TRASH")]
    [InlineData("drafts")]
    public void IsFolderIgnored_CaseInsensitive_ReturnsTrue(string folderName)
    {
        EmailSyncTask.IsFolderIgnored(folderName, DefaultKeywords).Should().BeTrue();
    }

    [Fact]
    public void IsFolderIgnored_EmptyKeywords_ReturnsFalse()
    {
        EmailSyncTask.IsFolderIgnored("Spam", Array.Empty<string>()).Should().BeFalse();
    }

    [Theory]
    [InlineData("[Gmail]/Spam")]
    [InlineData("[Gmail]/Trash")]
    [InlineData("[Gmail]/Drafts")]
    [InlineData("[Gmail]/Starred")]
    [InlineData("[Gmail]/Important")]
    public void IsFolderIgnored_GmailSpecialFolders_ReturnsTrue(string folderName)
    {
        EmailSyncTask.IsFolderIgnored(folderName, DefaultKeywords).Should().BeTrue();
    }

    [Fact]
    public void IsFolderIgnored_GmailInbox_ReturnsFalse()
    {
        EmailSyncTask.IsFolderIgnored("[Gmail]/INBOX", DefaultKeywords).Should().BeFalse();
    }

    [Theory]
    [InlineData("Deleted Items")]
    [InlineData("Junk Email")]
    public void IsFolderIgnored_OutlookFoldersContainingKeyword_ReturnsTrue(string folderName)
    {
        EmailSyncTask.IsFolderIgnored(folderName, DefaultKeywords).Should().BeTrue();
    }

    [Fact]
    public void GetIgnoredFolderKeywords_WithoutConfiguredAdditions_ReturnsBuiltInDefaults()
    {
        EmailSyncTask.GetIgnoredFolderKeywords(Array.Empty<string>()).Should().BeEquivalentTo(DefaultKeywords);
    }

    [Fact]
    public void GetIgnoredFolderKeywords_WithConfiguredAdditions_ExtendsBuiltInDefaults()
    {
        var keywords = EmailSyncTask.GetIgnoredFolderKeywords(new[] { "Invoices", "Spam", "  Follow Up  ", string.Empty, "   " });

        keywords.Should().Contain(DefaultKeywords);
        keywords.Should().Contain(new[] { "Invoices", "Follow Up" });
        keywords.Count(keyword => keyword.Equals("Spam", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    [Fact]
    public void IsFolderWhitelisted_EmptyWhitelist_ReturnsTrue()
    {
        EmailSyncTask.IsFolderWhitelisted("INBOX", Array.Empty<string>()).Should().BeTrue();
    }

    [Theory]
    [InlineData("INBOX")]
    [InlineData("INBOX/Leads")]
    [InlineData("Clients/Acme")]
    [InlineData("Clients/Acme/2026")]
    [InlineData("Projects.Active")]
    public void IsFolderWhitelisted_ExactMatchesAndChildren_ReturnsTrue(string folderName)
    {
        EmailSyncTask.IsFolderWhitelisted(folderName, DefaultWhitelist).Should().BeTrue();
    }

    [Theory]
    [InlineData("Archive")]
    [InlineData("Clients/Other")]
    [InlineData("Project")]
    [InlineData("Inboxes")]
    public void IsFolderWhitelisted_NonMatchingFolders_ReturnsFalse(string folderName)
    {
        EmailSyncTask.IsFolderWhitelisted(folderName, DefaultWhitelist).Should().BeFalse();
    }

    [Theory]
    [InlineData("INBOX")]
    [InlineData("INBOX/Leads")]
    public void ShouldSyncFolder_WhitelistedAndNotIgnored_ReturnsTrue(string folderName)
    {
        EmailSyncTask.ShouldSyncFolder(folderName, DefaultWhitelist, DefaultKeywords).Should().BeTrue();
    }

    [Theory]
    [InlineData("Spam")]
    [InlineData("Clients/Other")]
    [InlineData("INBOX/Spam")]
    public void ShouldSyncFolder_NotWhitelistedOrIgnored_ReturnsFalse(string folderName)
    {
        EmailSyncTask.ShouldSyncFolder(folderName, DefaultWhitelist, DefaultKeywords).Should().BeFalse();
    }
}
