// <copyright file="ILeadNotificationMessageBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Plugin.Site.DTOs;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Builds channel-specific lead notification payloads.
/// </summary>
public interface ILeadNotificationMessageBuilder
{
    /// <summary>
    /// Builds email template arguments from lead data.
    /// </summary>
    /// <param name="leadInfo">Lead notification info.</param>
    /// <returns>Template arguments dictionary.</returns>
    Dictionary<string, object> BuildEmailTemplateArguments(LeadNotificationInfo leadInfo);

    /// <summary>
    /// Builds plain text lead notification content for channels like Telegram and Slack.
    /// </summary>
    /// <param name="leadInfo">Lead notification info.</param>
    /// <returns>Notification message.</returns>
    string BuildTextMessage(LeadNotificationInfo leadInfo);
}
