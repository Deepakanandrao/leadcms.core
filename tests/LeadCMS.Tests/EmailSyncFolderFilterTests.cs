// <copyright file="EmailSyncFolderFilterTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using FluentAssertions;
using LeadCMS.EmailSync.Tasks;
using Xunit;

namespace LeadCMS.Tests;

public class EmailSyncFolderFilterTests
{
    private static readonly string[] DefaultKeywords = new[]
    {
        "Spam", "Junk", "Draft", "Archive",
        "Deleted", "Trash", "Bin",
        "Starred", "Important", "Flagged",
        "Bulk", "Clutter",
        "Conversation History", "Notes", "Calendar", "Contacts", "Tasks",
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
}
