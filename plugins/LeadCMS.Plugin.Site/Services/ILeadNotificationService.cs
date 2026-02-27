// <copyright file="ILeadNotificationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Plugin.Site.DTOs;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Service interface for sending lead capture notifications to various channels.
/// </summary>
public interface ILeadNotificationService
{
    /// <summary>
    /// Sends lead notification to all enabled channels (email, Telegram, Slack).
    /// </summary>
    /// <param name="leadInfo">The lead information to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendLeadNotificationsAsync(LeadNotificationInfo leadInfo);

    /// <summary>
    /// Sends lead notification by email using preloaded settings.
    /// </summary>
    /// <param name="leadInfo">The lead information to send.</param>
    /// <param name="settings">Preloaded lead capture settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendEmailNotificationAsync(LeadNotificationInfo leadInfo, List<Setting> settings);

    /// <summary>
    /// Sends lead notification to Telegram using preloaded settings.
    /// </summary>
    /// <param name="leadInfo">The lead information to send.</param>
    /// <param name="settings">Preloaded lead capture settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendTelegramNotificationAsync(LeadNotificationInfo leadInfo, List<Setting> settings);

    /// <summary>
    /// Sends lead notification to Slack using preloaded settings.
    /// </summary>
    /// <param name="leadInfo">The lead information to send.</param>
    /// <param name="settings">Preloaded lead capture settings.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendSlackNotificationAsync(LeadNotificationInfo leadInfo, List<Setting> settings);
}
