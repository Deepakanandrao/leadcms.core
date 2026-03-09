// <copyright file="ILeadNotificationMessageBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.Site.DTOs;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Extension point for lead notification message formatting.
/// Implementations can add computed fields (e.g. device summary from User-Agent)
/// to the template arguments built by <see cref="LeadNotificationInfo.ToTemplateArguments"/>.
/// </summary>
public interface ILeadNotificationMessageBuilder
{
    /// <summary>
    /// Enriches an existing template arguments dictionary with additional computed
    /// fields specific to the notification channel (e.g. parsed user-agent details).
    /// The <paramref name="args"/> dictionary is mutated in place.
    /// </summary>
    /// <param name="args">The template arguments dictionary to enrich.</param>
    /// <param name="leadInfo">The lead notification data.</param>
    void EnrichTemplateArguments(Dictionary<string, object> args, LeadNotificationInfo leadInfo);

    /// <summary>
    /// Builds plain text lead notification content for channels like Telegram and Slack.
    /// </summary>
    /// <param name="leadInfo">Lead notification info.</param>
    /// <returns>Notification message.</returns>
    string BuildTextMessage(LeadNotificationInfo leadInfo);
}
