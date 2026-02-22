// <copyright file="CampaignRecipient.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using LeadCMS.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Entities;

public enum CampaignRecipientStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3,
}

public enum CampaignSkipReason
{
    None = 0,
    Unsubscribed = 1,
    Duplicate = 2,
    Suppressed = 3,
    InvalidEmail = 4,
}

[Table("campaign_recipient")]
[Index(nameof(CampaignId), nameof(ContactId), IsUnique = true)]
public class CampaignRecipient : BaseEntityWithIdAndDates
{
    [Required]
    public int CampaignId { get; set; }

    [JsonIgnore]
    [ForeignKey("CampaignId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Campaign? Campaign { get; set; }

    [Required]
    public int ContactId { get; set; }

    [JsonIgnore]
    [ForeignKey("ContactId")]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual Contact? Contact { get; set; }

    [Required]
    public CampaignRecipientStatus Status { get; set; } = CampaignRecipientStatus.Pending;

    public CampaignSkipReason SkipReason { get; set; } = CampaignSkipReason.None;

    /// <summary>
    /// Gets or sets when the email was sent.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// Gets or sets the error message if sending failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
