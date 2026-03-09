// <copyright file="ContactServicePriorityMatchingTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class ContactServicePriorityMatchingTests : BaseTest
{
    public ContactServicePriorityMatchingTests()
    {
        TrackEntityType<Contact>();
        TrackEntityType<Domain>();
    }

    [Fact]
    public async Task FindOrCreateByIdentifiers_WhenEmailMissingButPhoneMatches_ReusesExistingContact()
    {
        var dbContext = App.GetDbContext()!;
        var existing = new Contact
        {
            Email = "known@example.test",
            Phone = "+15550001111",
            PhoneRaw = "+1 (555) 000-1111",
        };

        dbContext.Contacts!.Add(existing);
        await dbContext.SaveChangesAsync();

        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContactService>();
        var scopedDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var matched = await service.FindOrCreateByIdentifiers(
            "incoming@example.test",
            "+15550001111",
            "198.51.100.70",
            "PriorityAgent/1.0");
        await scopedDbContext.SaveChangesAsync();

        matched.Id.Should().Be(existing.Id);
        App.GetDbContext()!.Contacts!.Count().Should().Be(1);
    }

    [Fact]
    public async Task FindOrCreateByIdentifiers_PrefersEmailOverPhone()
    {
        var dbContext = App.GetDbContext()!;
        var emailMatch = new Contact
        {
            Email = "preferred@example.test",
            Phone = "+15550002222",
            PhoneRaw = "+1 (555) 000-2222",
        };
        var phoneMatch = new Contact
        {
            Email = "other@example.test",
            Phone = "+15550003333",
            PhoneRaw = "+1 (555) 000-3333",
        };

        dbContext.Contacts!.AddRange(emailMatch, phoneMatch);
        await dbContext.SaveChangesAsync();

        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContactService>();
        var scopedDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var matched = await service.FindOrCreateByIdentifiers(
            "preferred@example.test",
            "+15550003333",
            "198.51.100.71",
            "PriorityAgent/2.0");
        await scopedDbContext.SaveChangesAsync();

        matched.Id.Should().Be(emailMatch.Id);
        App.GetDbContext()!.Contacts!.Count().Should().Be(2);
    }

    [Fact]
    public async Task FindOrCreateByIdentifiers_MatchesEmailCaseInsensitively()
    {
        var dbContext = App.GetDbContext()!;
        var existing = new Contact
        {
            Email = "lowercase@example.test",
        };

        dbContext.Contacts!.Add(existing);
        await dbContext.SaveChangesAsync();

        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContactService>();
        var scopedDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var matched = await service.FindOrCreateByIdentifiers(
            "LowerCase@Example.Test",
            null,
            null,
            null);
        await scopedDbContext.SaveChangesAsync();

        matched.Id.Should().Be(existing.Id);
        matched.Email.Should().Be("lowercase@example.test");
        App.GetDbContext()!.Contacts!.Count().Should().Be(1);
    }
}
