// <copyright file="CampaignSendTask.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Interfaces;
using LeadCMS.Services;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Tasks;

/// <summary>
/// Background task that processes campaign sends.
/// - Picks up Scheduled campaigns at their send time, resolves audience, and starts sending.
/// - Picks up Sending campaigns and processes pending recipients in batches.
/// - Marks campaigns as Sent when all recipients have been processed.
/// </summary>
public class CampaignSendTask : BaseTask
{
    private readonly PgDbContext dbContext;
    private readonly ICampaignService campaignService;

    public CampaignSendTask(
        PgDbContext dbContext,
        ICampaignService campaignService,
        IConfiguration configuration,
        TaskStatusService taskStatusService)
        : base("Tasks:CampaignSendTask", configuration, taskStatusService)
    {
        this.dbContext = dbContext;
        this.campaignService = campaignService;
    }

    public override async Task<bool> Execute(TaskExecutionLog currentJob)
    {
        try
        {
            int scheduledProcessed = 0;
            int sendingProcessed = 0;
            int totalSent = 0;
            int totalFailed = 0;
            int totalSkipped = 0;
            int completedCampaigns = 0;

            // Step 1: Process scheduled campaigns that are due
            var scheduledCampaigns = await dbContext.Campaigns!
                .Where(c => c.Status == CampaignStatus.Scheduled && c.ScheduledAt != null)
                .ToListAsync();

            foreach (var campaign in scheduledCampaigns)
            {
                try
                {
                    // Convert ScheduledAt to UTC using the campaign's timezone offset.
                    // If UseContactTimeZone mode, use UTC+14 (840 min) — the earliest timezone on earth —
                    // so the campaign starts processing early enough for the first contacts to receive it.
                    var offsetMinutes = campaign.UseContactTimeZone ? 840 : (campaign.TimeZone ?? 0);
                    var scheduledUtc = campaignService.ConvertScheduledToUtc(campaign.ScheduledAt!.Value, offsetMinutes);

                    if (scheduledUtc > DateTime.UtcNow)
                    {
                        continue;
                    }

                    campaign.Status = CampaignStatus.Sending;
                    campaign.SendStartedAt = DateTime.UtcNow;

                    var recipientCount = await campaignService.ResolveAudienceAsync(campaign);
                    campaign.TotalRecipients = recipientCount;

                    await dbContext.SaveChangesAsync();
                    scheduledProcessed++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to start scheduled campaign Id={campaign.Id}");
                }
            }

            // Step 2: Process sending campaigns (send pending recipients in batches)
            var sendingCampaigns = await dbContext.Campaigns!
                .Where(c => c.Status == CampaignStatus.Sending)
                .ToListAsync();

            foreach (var campaign in sendingCampaigns)
            {
                try
                {
                    var (sent, failed, skipped) = await campaignService.ProcessRecipientsAsync(campaign, batchSize: 100);
                    totalSent += sent;
                    totalFailed += failed;
                    totalSkipped += skipped;
                    sendingProcessed++;

                    // Check if all recipients have been processed
                    var pendingCount = await dbContext.CampaignRecipients!
                        .CountAsync(r => r.CampaignId == campaign.Id && r.Status == CampaignRecipientStatus.Pending);

                    if (pendingCount == 0)
                    {
                        campaign.Status = CampaignStatus.Sent;
                        campaign.SendCompletedAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync();
                        completedCampaigns++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to process campaign Id={campaign.Id}");
                }
            }

            currentJob.Result = $"Scheduled: {scheduledProcessed} started, Sending: {sendingProcessed} processed ({totalSent} sent, {totalFailed} failed, {totalSkipped} skipped), {completedCampaigns} completed";
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error occurred when executing campaign send task in task runner {currentJob.Id}");
            currentJob.Result = $"Task execution failed: {ex.Message}";
            return false;
        }
    }
}
