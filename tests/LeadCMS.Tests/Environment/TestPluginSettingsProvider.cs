// <copyright file="TestPluginSettingsProvider.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Interfaces;

namespace LeadCMS.Tests.Environment;

/// <summary>
/// Test plugin settings provider that mimics what a real plugin (e.g., Site plugin) would register.
/// Used to validate the IPluginSettingsProvider pipeline in integration tests.
/// </summary>
public class TestPluginSettingsProvider : IPluginSettingsProvider
{
    /// <inheritdoc/>
    public IEnumerable<PluginSettingDefinition> GetSettingDefinitions()
    {
        yield return new PluginSettingDefinition
        {
            Key = "LeadCapture.Email.Enabled",
            DefaultValue = "false",
            Type = "bool",
            Description = "Whether email notifications are enabled for lead capture.",
        };
        yield return new PluginSettingDefinition
        {
            Key = "LeadCapture.Email.Recipients",
            DefaultValue = "[]",
            Type = "json",
            Description = "JSON array of email addresses to send lead notifications to.",
        };
        yield return new PluginSettingDefinition
        {
            Key = "LeadCapture.Telegram.Enabled",
            DefaultValue = "false",
            Type = "bool",
            Description = "Whether Telegram notifications are enabled for lead capture.",
        };
        yield return new PluginSettingDefinition
        {
            Key = "LeadCapture.Telegram.BotId",
            DefaultValue = string.Empty,
            Type = "string",
            Required = true,
            Description = "The Telegram bot ID for sending lead notifications.",
        };
        yield return new PluginSettingDefinition
        {
            Key = "LeadCapture.Telegram.ChatId",
            DefaultValue = string.Empty,
            Type = "string",
            Required = true,
            Description = "The Telegram chat ID to send lead notifications to.",
        };
        yield return new PluginSettingDefinition
        {
            Key = "LeadCapture.Slack.Enabled",
            DefaultValue = "false",
            Type = "bool",
            Description = "Whether Slack notifications are enabled for lead capture.",
        };
        yield return new PluginSettingDefinition
        {
            Key = "LeadCapture.Slack.WebhookUrl",
            DefaultValue = string.Empty,
            Type = "string",
            Required = true,
            Description = "The Slack incoming webhook URL for sending lead notifications.",
        };
    }
}
