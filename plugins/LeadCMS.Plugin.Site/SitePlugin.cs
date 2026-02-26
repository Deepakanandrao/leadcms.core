// <copyright file="SitePlugin.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Interfaces;
using LeadCMS.Plugin.Site.Configuration;
using LeadCMS.Plugin.Site.Data;
using LeadCMS.Plugin.Site.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadCMS.Plugin.Site;

/// <summary>
/// Site plugin providing website functionality including contact forms, subscriptions, and lead capture.
/// </summary>
public class SitePlugin : IPlugin, ICapabilityProvider, ISettingsProvider
{
    public static PluginSettings Settings { get; private set; } = new PluginSettings();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginSettings = configuration.Get<PluginSettings>();

        if (pluginSettings != null)
        {
            Settings = pluginSettings;
        }

        services.AddScoped<PluginDbContextBase, LeadCmsSiteDbContext>();
        services.AddScoped<LeadCmsSiteDbContext, LeadCmsSiteDbContext>();
        services.AddScoped<ILeadNotificationService, LeadNotificationService>();
        services.AddSingleton<ISubscriptionTokenService>(_ =>
            new SubscriptionTokenService(Settings.SubscriptionTokenSecret));
    }

    /// <inheritdoc/>
    public IEnumerable<string> GetCapabilities()
    {
        yield return "Site";
    }

    /// <inheritdoc/>
    public IEnumerable<SettingDefinition> GetSettingDefinitions()
    {
        yield return new SettingDefinition
        {
            Key = LeadCaptureSettingKeys.EmailEnabled,
            DefaultValue = "false",
            Type = "bool",
            Description = "Whether email notifications are enabled for lead capture.",
        };
        yield return new SettingDefinition
        {
            Key = LeadCaptureSettingKeys.EmailRecipients,
            DefaultValue = "[]",
            Type = "email[]",
            Description = "Array of email addresses to send lead notifications to.",
        };
        yield return new SettingDefinition
        {
            Key = LeadCaptureSettingKeys.TelegramEnabled,
            DefaultValue = "false",
            Type = "bool",
            Description = "Whether Telegram notifications are enabled for lead capture.",
        };
        yield return new SettingDefinition
        {
            Key = LeadCaptureSettingKeys.TelegramBotId,
            DefaultValue = string.Empty,
            Type = "string",
            Required = true,
            Description = "The Telegram bot ID for sending lead notifications.",
        };
        yield return new SettingDefinition
        {
            Key = LeadCaptureSettingKeys.TelegramChatId,
            DefaultValue = string.Empty,
            Type = "string",
            Required = true,
            Description = "The Telegram chat ID to send lead notifications to.",
        };
        yield return new SettingDefinition
        {
            Key = LeadCaptureSettingKeys.SlackEnabled,
            DefaultValue = "false",
            Type = "bool",
            Description = "Whether Slack notifications are enabled for lead capture.",
        };
        yield return new SettingDefinition
        {
            Key = LeadCaptureSettingKeys.SlackWebhookUrl,
            DefaultValue = string.Empty,
            Type = "string",
            Required = true,
            Description = "The Slack incoming webhook URL for sending lead notifications.",
        };
    }
}