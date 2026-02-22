// <copyright file="CampaignScheduleHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Helpers;

public static class CampaignScheduleHelper
{
    public static DateTime ConvertScheduledLocalToUtc(DateTime scheduledAt, int timeZoneOffsetMinutes)
    {
        return scheduledAt.AddMinutes(-timeZoneOffsetMinutes);
    }

    public static DateTime? GetExpectedSendAtUtc(Campaign campaign, Contact? contact)
    {
        if (campaign.ScheduledAt == null)
        {
            return null;
        }

        if (!campaign.UseContactTimeZone)
        {
            return campaign.ScheduledAt.Value;
        }

        var campaignOffset = campaign.TimeZone ?? 0;
        var recipientOffset = contact?.Timezone ?? campaignOffset;

        return campaign.ScheduledAt.Value.AddMinutes(recipientOffset - campaignOffset);
    }

    public static DateTime? GetExpectedSendAtUtc(CampaignRecipient recipient)
    {
        if (recipient.SentAt.HasValue)
        {
            return recipient.SentAt;
        }

        if (recipient.Status != CampaignRecipientStatus.Pending)
        {
            return null;
        }

        if (recipient.Campaign == null)
        {
            return null;
        }

        return GetExpectedSendAtUtc(recipient.Campaign, recipient.Contact);
    }
}
