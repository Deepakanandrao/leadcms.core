// <copyright file="EmailSyncContactCreationTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.EmailSync.Tasks;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.EmailSync.Data;
using LeadCMS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class EmailSyncContactCreationTests : BaseTestAutoLogin
{
    public EmailSyncContactCreationTests()
    {
        TrackEntityType<Contact>();
        TrackEntityType<Domain>();
    }

    [Fact]
    public async Task EnrichWithContactIdAsync_WhenAutoCreateEnabled_AssignsConfiguredTags()
    {
        var task = await CreateTaskAsync(
            createContactsForUnknownEmails: true,
            autoCreatedContactTags: new[] { "Imported", "  Prospect  ", "Imported" });

        var emailLog = new EmailLog
        {
            FromEmail = "lead@example.com",
            Recipients = "team@waveservice.app",
            Subject = "New lead",
            MessageId = Guid.NewGuid().ToString("N"),
        };

        var createdContacts = await task.EnrichWithContactIdAsync(new List<EmailLog> { emailLog });

        createdContacts.Should().Be(1);
        emailLog.Contact.Should().NotBeNull();
        emailLog.Contact!.Email.Should().Be("lead@example.com");
        emailLog.Contact.Tags.Should().BeEquivalentTo("Imported", "Prospect");
    }

    [Fact]
    public async Task EnrichWithContactIdAsync_WhenAutoCreateDisabled_DoesNotCreateUnknownContacts()
    {
        var task = await CreateTaskAsync(createContactsForUnknownEmails: false, autoCreatedContactTags: new[] { "Imported" });

        var emailLog = new EmailLog
        {
            FromEmail = "lead@example.com",
            Recipients = "team@waveservice.app",
            Subject = "New lead",
            MessageId = Guid.NewGuid().ToString("N"),
        };

        var createdContacts = await task.EnrichWithContactIdAsync(new List<EmailLog> { emailLog });

        createdContacts.Should().Be(0);
        emailLog.ContactId.Should().BeNull();
        emailLog.Contact.Should().BeNull();
    }

    [Fact]
    public async Task EnrichWithContactIdAsync_WhenAutoCreateDisabled_RemovesLogsWithoutResolvedContacts()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var pgDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        var existingContact = new Contact
        {
            Email = "known@example.com",
        };

        await pgDbContext.Contacts!.AddAsync(existingContact);
        await pgDbContext.SaveChangesAsync();

        var task = await CreateTaskAsync(createContactsForUnknownEmails: false, autoCreatedContactTags: Array.Empty<string>());
        var emailLogs = new List<EmailLog>
        {
            new EmailLog
            {
                FromEmail = "known@example.com",
                Recipients = "team@waveservice.app",
                Subject = "Known lead",
                MessageId = Guid.NewGuid().ToString("N"),
            },
            new EmailLog
            {
                FromEmail = "unknown@example.com",
                Recipients = "team@waveservice.app",
                Subject = "Unknown lead",
                MessageId = Guid.NewGuid().ToString("N"),
            },
        };

        var createdContacts = await task.EnrichWithContactIdAsync(emailLogs);

        createdContacts.Should().Be(0);
        emailLogs.Should().HaveCount(1);
        emailLogs[0].FromEmail.Should().Be("known@example.com");
        emailLogs[0].Contact.Should().NotBeNull();
        emailLogs[0].Contact!.Id.Should().Be(existingContact.Id);
    }

    [Fact]
    public async Task EnrichWithContactIdAsync_WhenAutoCreateDisabled_UsesExistingContacts()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var pgDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        var existingContact = new Contact
        {
            Email = "lead@example.com",
        };

        await pgDbContext.Contacts!.AddAsync(existingContact);
        await pgDbContext.SaveChangesAsync();

        var task = await CreateTaskAsync(createContactsForUnknownEmails: false, autoCreatedContactTags: Array.Empty<string>());

        var emailLog = new EmailLog
        {
            FromEmail = "lead@example.com",
            Recipients = "team@waveservice.app",
            Subject = "Existing lead",
            MessageId = Guid.NewGuid().ToString("N"),
        };

        var createdContacts = await task.EnrichWithContactIdAsync(new List<EmailLog> { emailLog });

        createdContacts.Should().Be(0);
        emailLog.Contact.Should().NotBeNull();
        emailLog.Contact!.Email.Should().Be(existingContact.Email);
        emailLog.Contact.Id.Should().Be(existingContact.Id);
    }

    [Fact]
    public async Task EnrichWithContactIdAsync_WhenIncomingEmailHasDifferentCase_UsesExistingContact()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var pgDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        var existingContact = new Contact
        {
            Email = "lead@example.com",
        };

        await pgDbContext.Contacts!.AddAsync(existingContact);
        await pgDbContext.SaveChangesAsync();

        var task = await CreateTaskAsync(createContactsForUnknownEmails: false, autoCreatedContactTags: Array.Empty<string>());

        var emailLog = new EmailLog
        {
            FromEmail = "Lead@Example.com",
            Recipients = "team@waveservice.app",
            Subject = "Existing lead mixed case",
            MessageId = Guid.NewGuid().ToString("N"),
        };

        var createdContacts = await task.EnrichWithContactIdAsync(new List<EmailLog> { emailLog });

        createdContacts.Should().Be(0);
        emailLog.Contact.Should().NotBeNull();
        emailLog.Contact!.Email.Should().Be(existingContact.Email);
        emailLog.Contact.Id.Should().Be(existingContact.Id);
    }

    private async Task<EmailSyncTask> CreateTaskAsync(bool createContactsForUnknownEmails, string[] autoCreatedContactTags)
    {
        var scope = App.Services.CreateScope();
        var pgDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        var httpContextHelper = scope.ServiceProvider.GetRequiredService<IHttpContextHelper>();
        var baseConfiguration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var domainService = scope.ServiceProvider.GetRequiredService<IDomainService>();
        var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(baseConfiguration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailSync:EncryptionKey"] = "test-key-123456!",
                ["EmailSync:InternalDomains:0"] = "waveservice.app",
                ["EmailSync:CreateContactsForUnknownEmails"] = createContactsForUnknownEmails.ToString(),
                ["Tasks:EmailSyncTask:Enable"] = bool.FalseString,
                ["Tasks:EmailSyncTask:CronSchedule"] = "0/30 * * * * ?",
                ["Tasks:EmailSyncTask:RetryCount"] = "2",
                ["Tasks:EmailSyncTask:RetryInterval"] = "1",
                ["Tasks:EmailSyncTask:BatchSize"] = "20",
            }
            .Concat(autoCreatedContactTags.Select((tag, index) => new KeyValuePair<string, string?>("EmailSync:AutoCreatedContactTags:" + index, tag))))
            .Build();

        var emailSyncDbOptions = new DbContextOptionsBuilder<PgDbContext>()
            .UseNpgsql(
                pgDbContext.Database.GetDbConnection().ConnectionString,
                options => options.MigrationsAssembly(typeof(EmailSyncDbContext).Assembly.FullName))
            .Options;

        var emailSyncDbContext = new EmailSyncDbContext(emailSyncDbOptions, configuration, httpContextHelper);
        await emailSyncDbContext.Database.MigrateAsync();

        return new EmailSyncTask(
            configuration,
            emailSyncDbContext,
            new TaskStatusService(),
            domainService,
            contactService);
    }
}