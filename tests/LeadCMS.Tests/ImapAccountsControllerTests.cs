// <copyright file="ImapAccountsControllerTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using AutoMapper;
using LeadCMS.Data;
using LeadCMS.Exceptions;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.EmailSync.Configuration;
using LeadCMS.Plugin.EmailSync.Controllers;
using LeadCMS.Plugin.EmailSync.Data;
using LeadCMS.Plugin.EmailSync.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Tests;

public class ImapAccountsControllerTests : BaseTestAutoLogin
{
    [Fact]
    public async Task PostAccountForUser_WithProductionPlaceholderKey_ShouldFail()
    {
        using var scope = App.Services.CreateScope();
        var pgDbContext = scope.ServiceProvider.GetRequiredService<PgDbContext>();
        var httpContextHelper = scope.ServiceProvider.GetRequiredService<IHttpContextHelper>();
        var baseConfiguration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(baseConfiguration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["EmailSync:EncryptionKey"] = EmailSyncConfigurationValidator.EncryptionKeyPlaceholder,
            })
            .Build();

        var emailSyncDbOptions = new DbContextOptionsBuilder<PgDbContext>()
            .UseNpgsql(
                pgDbContext.Database.GetDbConnection().ConnectionString,
                options => options.MigrationsAssembly(typeof(EmailSyncDbContext).Assembly.FullName))
            .Options;

        await using var emailSyncDbContext = new EmailSyncDbContext(emailSyncDbOptions, configuration, httpContextHelper);

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>()).CreateMapper();
        var controller = new ImapAccountsController(emailSyncDbContext, mapper, configuration);

        var action = async () => await controller.PostAccountForUser("admin", new ImapAccountCreateDto
        {
            Host = "imap.example.com",
            UserName = "admin@example.com",
            Password = "secret",
            Port = 993,
            UseSsl = true,
        });

        var exception = await action.Should().ThrowAsync<UnprocessableEntityException>();
        exception.Which.Message.Should().Contain("placeholder value");
        exception.Which.Message.Should().Contain(EmailSyncConfigurationValidator.EncryptionKeyPlaceholder);
    }
}