// <copyright file="ContactMergeHelperTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Helpers;

namespace LeadCMS.Tests;

public class ContactMergeHelperTests
{
    private const string Source = "ContactForm";
    private const string Ip = "127.0.0.1";
    private const string UserAgent = "TestAgent/1.0";

    [Fact]
    public void ApplyPublicUpdate_WhenFieldIsNull_SetsValueDirectly()
    {
        var contact = new Contact();
        string? captured = null;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            null,
            "Alice",
            v => captured = v,
            Source,
            Ip,
            UserAgent);

        captured.Should().Be("Alice");
        contact.PendingUpdates.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyPublicUpdate_WhenFieldIsEmpty_SetsValueDirectly()
    {
        var contact = new Contact();
        string? captured = null;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            string.Empty,
            "Bob",
            v => captured = v,
            Source,
            Ip,
            UserAgent);

        captured.Should().Be("Bob");
        contact.PendingUpdates.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyPublicUpdate_WhenFieldIsWhitespace_SetsValueDirectly()
    {
        var contact = new Contact();
        string? captured = null;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            "   ",
            "Charlie",
            v => captured = v,
            Source,
            Ip,
            UserAgent);

        captured.Should().Be("Charlie");
        contact.PendingUpdates.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyPublicUpdate_WhenProposedIsNull_DoesNothing()
    {
        var contact = new Contact();
        bool setterCalled = false;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            "Alice",
            null,
            _ => setterCalled = true,
            Source,
            Ip,
            UserAgent);

        setterCalled.Should().BeFalse();
        contact.PendingUpdates.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyPublicUpdate_WhenProposedIsEmpty_DoesNothing()
    {
        var contact = new Contact();
        bool setterCalled = false;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            "Alice",
            "  ",
            _ => setterCalled = true,
            Source,
            Ip,
            UserAgent);

        setterCalled.Should().BeFalse();
        contact.PendingUpdates.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyPublicUpdate_WhenSameValue_DoesNothing()
    {
        var contact = new Contact();
        bool setterCalled = false;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            "Alice",
            "Alice",
            _ => setterCalled = true,
            Source,
            Ip,
            UserAgent);

        setterCalled.Should().BeFalse();
        contact.PendingUpdates.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyPublicUpdate_WhenSameValueDifferentCase_DoesNothing()
    {
        var contact = new Contact();
        bool setterCalled = false;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            "alice",
            "ALICE",
            _ => setterCalled = true,
            Source,
            Ip,
            UserAgent);

        setterCalled.Should().BeFalse();
        contact.PendingUpdates.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ApplyPublicUpdate_WhenDifferentValue_AddsToPendingUpdates()
    {
        var contact = new Contact();
        bool setterCalled = false;

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            "Alice",
            "Bob",
            _ => setterCalled = true,
            Source,
            Ip,
            UserAgent);

        setterCalled.Should().BeFalse();
        contact.PendingUpdates.Should().HaveCount(1);

        var pending = contact.PendingUpdates![0];
        pending.Field.Should().Be(nameof(Contact.FirstName));
        pending.OldValue.Should().Be("Alice");
        pending.ProposedValue.Should().Be("Bob");
        pending.Source.Should().Be(Source);
        pending.Ip.Should().Be(Ip);
        pending.UserAgent.Should().Be(UserAgent);
        pending.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ApplyPublicUpdate_MultipleConflicts_AccumulateInPendingUpdates()
    {
        var contact = new Contact { FirstName = "Alice", LastName = "Smith" };

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            contact.FirstName,
            "Bob",
            v => contact.FirstName = v,
            Source,
            Ip,
            UserAgent);

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.LastName),
            contact.LastName,
            "Jones",
            v => contact.LastName = v,
            Source,
            Ip,
            UserAgent);

        contact.FirstName.Should().Be("Alice", "original value should not change");
        contact.LastName.Should().Be("Smith", "original value should not change");
        contact.PendingUpdates.Should().HaveCount(2);
        contact.PendingUpdates![0].Field.Should().Be(nameof(Contact.FirstName));
        contact.PendingUpdates[1].Field.Should().Be(nameof(Contact.LastName));
    }

    [Fact]
    public void ApplyPublicUpdate_PreservesExistingPendingUpdates()
    {
        var contact = new Contact
        {
            PendingUpdates = new List<PendingContactUpdate>
            {
                new() { Field = "CompanyName", OldValue = "OldCo", ProposedValue = "NewCo" },
            },
        };

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.FirstName),
            "Alice",
            "Charlie",
            v => contact.FirstName = v,
            Source,
            Ip,
            UserAgent);

        contact.PendingUpdates.Should().HaveCount(2);
        contact.PendingUpdates[0].Field.Should().Be("CompanyName");
        contact.PendingUpdates[1].Field.Should().Be(nameof(Contact.FirstName));
    }

    [Fact]
    public void ApplyPublicUpdate_WithNullIpAndUserAgent_StillStoresPending()
    {
        var contact = new Contact();

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(Contact.Source),
            "OldSource",
            "NewSource",
            v => contact.Source = v,
            "Subscribe",
            null,
            null);

        contact.PendingUpdates.Should().HaveCount(1);
        contact.PendingUpdates![0].Ip.Should().BeNull();
        contact.PendingUpdates[0].UserAgent.Should().BeNull();
    }
}
