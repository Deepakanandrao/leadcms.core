// <copyright file="CampaignSchedulePastException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Exceptions;

/// <summary>
/// Thrown when a campaign's scheduled time is already in the past for some
/// recipients based on their individual timezones.
/// </summary>
public class CampaignSchedulePastException : UnprocessableEntityException
{
    public CampaignSchedulePastException(
        string message,
        int affectedRecipientCount,
        int totalRecipientCount,
        int earliestOffsetMinutes)
        : base(message)
    {
        AffectedRecipientCount = affectedRecipientCount;
        TotalRecipientCount = totalRecipientCount;
        EarliestOffsetMinutes = earliestOffsetMinutes;

        AddExtension("affectedRecipientCount", affectedRecipientCount);
        AddExtension("totalRecipientCount", totalRecipientCount);
        AddExtension("earliestOffsetMinutes", earliestOffsetMinutes);
    }

    /// <summary>
    /// Gets the number of recipients whose local scheduled time is already in the past.
    /// </summary>
    public int AffectedRecipientCount { get; }

    /// <summary>
    /// Gets the total number of recipients in the audience.
    /// </summary>
    public int TotalRecipientCount { get; }

    /// <summary>
    /// Gets the UTC offset (in minutes) of the earliest (most ahead) timezone in the audience.
    /// </summary>
    public int EarliestOffsetMinutes { get; }
}
