// <copyright file="ICampaignService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Entities;

namespace LeadCMS.Interfaces;

public interface ICampaignService
{
    /// <summary>
    /// Launches a campaign: transitions it from Draft to Sending (immediate) or Scheduled.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <param name="launchDto">The launch configuration.</param>
    /// <returns>The updated campaign.</returns>
    Task<Campaign> LaunchAsync(int campaignId, CampaignLaunchDto launchDto);

    /// <summary>
    /// Cancels a scheduled campaign before it starts sending.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <returns>The updated campaign.</returns>
    Task<Campaign> CancelAsync(int campaignId);

    /// <summary>
    /// Pauses a campaign that is currently sending.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <returns>The updated campaign.</returns>
    Task<Campaign> PauseAsync(int campaignId);

    /// <summary>
    /// Resumes a paused campaign.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <returns>The updated campaign.</returns>
    Task<Campaign> ResumeAsync(int campaignId);

    /// <summary>
    /// Resolves the audience for a campaign: evaluates segments, deduplicates, checks unsubscribes,
    /// and creates CampaignRecipient records.
    /// </summary>
    /// <param name="campaign">The campaign to resolve audience for.</param>
    /// <returns>The total number of recipients.</returns>
    Task<int> ResolveAudienceAsync(Campaign campaign);

    /// <summary>
    /// Processes pending recipients for a campaign: sends emails in batches.
    /// </summary>
    /// <param name="campaign">The campaign to process.</param>
    /// <param name="batchSize">The number of recipients to process per batch.</param>
    /// <returns>A tuple of sent, failed, and skipped counts.</returns>
    Task<(int sent, int failed, int skipped)> ProcessRecipientsAsync(Campaign campaign, int batchSize = 100);

    /// <summary>
    /// Gets campaign statistics including skip reason breakdown.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <returns>The campaign statistics.</returns>
    Task<CampaignStatisticsDto> GetStatisticsAsync(int campaignId);

    /// <summary>
    /// Generates a campaign preview including audience statistics (total, sendable, unsubscribed,
    /// invalid email counts) and a rendered email template preview.
    /// </summary>
    /// <param name="dto">The preview request containing template, segments, optional contact ID, and optional custom variables.</param>
    /// <returns>The campaign preview result with audience breakdown and rendered template preview.</returns>
    Task<CampaignPreviewResultDto> PreviewAsync(CampaignPreviewRequestDto dto);

    /// <summary>
    /// Converts a campaign's ScheduledAt from the campaign timezone to UTC.
    /// </summary>
    /// <param name="scheduledAt">The scheduled date/time in the campaign's timezone.</param>
    /// <param name="timeZoneOffsetMinutes">The UTC offset in minutes (e.g. 120 for UTC+2).</param>
    /// <returns>The UTC equivalent of the scheduled time.</returns>
    DateTime ConvertScheduledToUtc(DateTime scheduledAt, int timeZoneOffsetMinutes);
}
