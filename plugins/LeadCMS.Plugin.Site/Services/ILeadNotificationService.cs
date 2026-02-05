// <copyright file="ILeadNotificationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

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
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendLeadNotificationAsync(LeadNotificationInfo leadInfo, CancellationToken cancellationToken = default);
}
