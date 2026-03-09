// <copyright file="ContactServicePotentialMatchingTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class ContactServicePotentialMatchingTests : BaseTest
{
    public ContactServicePotentialMatchingTests()
    {
        TrackEntityType<Contact>();
        TrackEntityType<Domain>();
    }

    [Fact]
    public async Task FindOrCreatePotential_WithIpAndUserAgent_CreatesPotentialContact()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContactService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var contact = await service.FindOrCreatePotential("198.51.100.42", "PotentialAgent/1.0");
        await dbContext.SaveChangesAsync();

        contact.Email.Should().BeNull();
        contact.Phone.Should().BeNull();
    }

    [Fact]
    public async Task FindOrCreate_WithNoEmailMatch_FallsBackToPotentialContactByIpAndUserAgent()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContactService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var potential = new Contact
        {
            CreatedByIp = "198.51.100.52",
            CreatedByUserAgent = "PotentialAgent/2.0",
        };
        dbContext.Contacts!.Add(potential);
        await dbContext.SaveChangesAsync();

        var contact = await service.FindOrCreate(
            "matched-potential@example.test",
            "198.51.100.52",
            "PotentialAgent/2.0");
        await dbContext.SaveChangesAsync();

        contact.Id.Should().Be(potential.Id);
        contact.Email.Should().Be("matched-potential@example.test");
    }

    [Fact]
    public async Task FindOrCreateByPhone_WithNoPhoneMatch_FallsBackToPotentialContactByIpAndUserAgent()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContactService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var potential = new Contact
        {
            CreatedByIp = "198.51.100.62",
            CreatedByUserAgent = "PotentialAgent/3.0",
        };
        dbContext.Contacts!.Add(potential);
        await dbContext.SaveChangesAsync();

        var contact = await service.FindOrCreateByPhone(
            "+1-555-7788",
            "198.51.100.62",
            "PotentialAgent/3.0");
        await dbContext.SaveChangesAsync();

        contact.Id.Should().Be(potential.Id);
        contact.PhoneRaw.Should().Be("+1-555-7788");
    }

    [Fact]
    public async Task FindOrCreatePotential_WhenPotentialExists_ReusesExistingPotentialContact()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContactService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();

        var potential = new Contact
        {
            CreatedByIp = "198.51.100.72",
            CreatedByUserAgent = "PotentialAgent/4.0",
        };
        dbContext.Contacts!.Add(potential);
        await dbContext.SaveChangesAsync();

        var matched = await service.FindOrCreatePotential("198.51.100.72", "PotentialAgent/4.0");
        await dbContext.SaveChangesAsync();

        matched.Id.Should().Be(potential.Id);
    }
}
