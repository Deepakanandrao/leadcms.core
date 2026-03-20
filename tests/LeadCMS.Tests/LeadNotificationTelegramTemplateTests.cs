// <copyright file="LeadNotificationTelegramTemplateTests.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.Site.Configuration;
using LeadCMS.Plugin.Site.DTOs;
using LeadCMS.Plugin.Site.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeadCMS.Tests;

public class LeadNotificationTelegramTemplateTests
{
    [Fact]
    public async Task BuildTelegramMessageAsync_WithoutTemplateSetting_FallsBackToHardcodedMessage()
    {
        var service = CreateService();
        var leadInfo = new LeadNotificationInfo
        {
            Title = "New signup",
            Email = "test@example.com",
            FirstName = "Jane",
            LastName = "Doe",
        };
        var settings = new List<Setting>();

        var message = await service.BuildTelegramMessageAsync(leadInfo, settings);

        message.Should().Contain("\ud83d\udce9 New signup");
        message.Should().Contain("Jane Doe");
        message.Should().Contain("test@example.com");
    }

    [Fact]
    public async Task BuildTelegramMessageAsync_WithEmptyTemplateSetting_FallsBackToHardcodedMessage()
    {
        var service = CreateService();
        var leadInfo = new LeadNotificationInfo
        {
            Email = "test@example.com",
            FirstName = "Ada",
        };
        var settings = new List<Setting>
        {
            new Setting { Key = LeadCaptureSettingKeys.TelegramMessageTemplate, Value = string.Empty },
        };

        var message = await service.BuildTelegramMessageAsync(leadInfo, settings);

        message.Should().Contain("\ud83d\udce9 New lead captured");
        message.Should().Contain("Ada");
        message.Should().Contain("test@example.com");
    }

    [Fact]
    public async Task BuildTelegramMessageAsync_WithLiquidTemplate_RendersTemplate()
    {
        var service = CreateService();
        var leadInfo = new LeadNotificationInfo
        {
            Title = "Demo request",
            Email = "lead@example.com",
            FirstName = "Bob",
            LastName = "Smith",
            Phone = "+1234567890",
            CompanyName = "Acme Inc",
        };
        var settings = new List<Setting>
        {
            new Setting
            {
                Key = LeadCaptureSettingKeys.TelegramMessageTemplate,
                Value = "\ud83d\udd14 {{ Title }}\nName: {{ FullName }}\nEmail: {{ Email }}\nPhone: {{ Phone }}\nCompany: {{ CompanyName }}",
            },
        };

        var message = await service.BuildTelegramMessageAsync(leadInfo, settings);

        message.Should().Contain("\ud83d\udd14 Demo request");
        message.Should().Contain("Name: Bob Smith");
        message.Should().Contain("Email: lead@example.com");
        message.Should().Contain("Phone: +1234567890");
        message.Should().Contain("Company: Acme Inc");
    }

    [Fact]
    public async Task BuildTelegramMessageAsync_WithLiquidTemplate_SupportsConditionals()
    {
        var service = CreateService();
        var leadInfo = new LeadNotificationInfo
        {
            Email = "test@example.com",
            FirstName = "Alice",
        };
        var settings = new List<Setting>
        {
            new Setting
            {
                Key = LeadCaptureSettingKeys.TelegramMessageTemplate,
                Value = "Lead: {{ Email }}{% if Phone %}\nPhone: {{ Phone }}{% endif %}{% if CompanyName %}\nCompany: {{ CompanyName }}{% endif %}",
            },
        };

        var message = await service.BuildTelegramMessageAsync(leadInfo, settings);

        message.Should().Contain("Lead: test@example.com");
        message.Should().NotContain("Phone:");
        message.Should().NotContain("Company:");
    }

    [Fact]
    public async Task BuildTelegramMessageAsync_WithLiquidTemplate_SupportsExtraData()
    {
        var service = CreateService();
        var leadInfo = new LeadNotificationInfo
        {
            Email = "test@example.com",
            ExtraData = new Dictionary<string, string>
            {
                ["ProductInterest"] = "Enterprise",
                ["Source"] = "Webinar",
            },
        };
        var settings = new List<Setting>
        {
            new Setting
            {
                Key = LeadCaptureSettingKeys.TelegramMessageTemplate,
                Value = "\ud83d\udce9 New lead\nEmail: {{ Email }}\nProduct: {{ ProductInterest }}\nSource: {{ Source }}",
            },
        };

        var message = await service.BuildTelegramMessageAsync(leadInfo, settings);

        message.Should().Contain("Product: Enterprise");
        message.Should().Contain("Source: Webinar");
    }

    [Fact]
    public async Task BuildTelegramMessageAsync_WithLiquidTemplate_RestoresNewlinesFromBrTags()
    {
        var service = CreateService();
        var leadInfo = new LeadNotificationInfo
        {
            Email = "test@example.com",
            Message = "Line one\nLine two\nLine three",
        };
        var settings = new List<Setting>
        {
            new Setting
            {
                Key = LeadCaptureSettingKeys.TelegramMessageTemplate,
                Value = "Message:\n{{ Message }}",
            },
        };

        var message = await service.BuildTelegramMessageAsync(leadInfo, settings);

        // LiquidTemplateService converts \n to <br /> in string values;
        // BuildTelegramMessageAsync should restore plain-text newlines.
        message.Should().NotContain("<br />");
        message.Should().Contain("Line one\nLine two\nLine three");
    }

    private static LeadNotificationService CreateService(
        ILiquidTemplateService? liquidTemplateService = null)
    {
        var stubEmailService = new StubEmailFromTemplateService();
        var stubSettingService = new StubSettingService();
        var configuration = new ConfigurationBuilder().Build();
        var messageBuilder = new DefaultLeadNotificationMessageBuilder();
        var templateService = liquidTemplateService ?? new StubLiquidTemplateService();
        var logger = NullLogger<LeadNotificationService>.Instance;

        return new LeadNotificationService(
            stubEmailService,
            stubSettingService,
            configuration,
            messageBuilder,
            templateService,
            logger);
    }

    /// <summary>
    /// Minimal stub for IEmailFromTemplateService — not exercised in these tests.
    /// </summary>
    private sealed class StubEmailFromTemplateService : IEmailFromTemplateService
    {
        public Task SendAsync(
            string templateName,
            string language,
            string[] recipients,
            Dictionary<string, object>? templateArguments,
            List<AttachmentDto>? attachments,
            int contactId = 0,
            int campaignId = 0)
            => Task.CompletedTask;

        public Task<int> SendToContactAsync(
            int contactId,
            string templateName,
            Dictionary<string, object>? templateArguments,
            List<AttachmentDto>? attachments,
            int scheduleId = 0,
            int campaignId = 0)
            => Task.FromResult(0);
    }

    /// <summary>
    /// Minimal stub for ISettingService — not exercised by BuildTelegramMessageAsync.
    /// </summary>
    private sealed class StubSettingService : ISettingService
    {
        public Task<string?> GetUserSettingAsync(string key, string userId, string? language = null)
            => Task.FromResult<string?>(null);

        public Task<string?> GetSystemSettingAsync(string key, string? language = null)
            => Task.FromResult<string?>(null);

        public Task<Setting?> FindSystemSettingAsync(string key, string? language = null)
            => Task.FromResult<Setting?>(null);

        public Task<Setting?> FindEffectiveUserSettingAsync(string key, string userId, string? language = null)
            => Task.FromResult<Setting?>(null);

        public Task<List<Setting>> GetEffectiveUserSettingEntitiesAsync(string userId, string? language = null)
            => Task.FromResult(new List<Setting>());

        public Task SetUserSettingAsync(string key, string? value, string userId) => Task.CompletedTask;

        public Task SetSystemSettingAsync(string key, string? value, string? language = null) => Task.CompletedTask;

        public Task DeleteUserSettingAsync(string key, string userId) => Task.CompletedTask;

        public Task DeleteSystemSettingAsync(string key, string? language = null) => Task.CompletedTask;

        public Task<List<Setting>> FindSettingsByKeysAsync(IEnumerable<string> keys, string? userId = null, string? language = null)
            => Task.FromResult(new List<Setting>());

        public Task<string?> GetSettingWithFallbackAsync(string key, string configurationPath, string? userId = null, string? language = null)
            => Task.FromResult<string?>(null);

        public Task<int> GetIntSettingWithFallbackAsync(string key, string configurationPath, int defaultValue = 0, string? userId = null, string? language = null)
            => Task.FromResult(defaultValue);

        public Task<int> GetIntSettingWithFallbackAsync(string settingKey, int defaultValue = 0, string? userId = null, string? language = null)
            => Task.FromResult(defaultValue);

        public Task<bool> GetBoolSettingWithFallbackAsync(string key, string configurationPath, bool defaultValue = false, string? userId = null, string? language = null)
            => Task.FromResult(defaultValue);

        public Task<bool> GetBoolSettingWithFallbackAsync(string settingKey, bool defaultValue = false, string? userId = null, string? language = null)
            => Task.FromResult(defaultValue);
    }

    /// <summary>
    /// Uses the real LiquidTemplateService with a minimal stub ISettingService and empty configuration
    /// so Liquid rendering actually works in unit tests.
    /// </summary>
    private sealed class StubLiquidTemplateService : ILiquidTemplateService
    {
        private readonly LeadCMS.Services.LiquidTemplateService inner;

        public StubLiquidTemplateService()
        {
            var stubSettingService = new StubSettingService();
            var configuration = new ConfigurationBuilder().Build();
            inner = new LeadCMS.Services.LiquidTemplateService(stubSettingService, configuration);
        }

        public Task<string> RenderAsync(string template, Dictionary<string, object>? variables)
            => inner.RenderAsync(template, variables);
    }
}
