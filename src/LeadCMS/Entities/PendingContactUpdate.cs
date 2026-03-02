// <copyright file="PendingContactUpdate.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Entities;

/// <summary>
/// Represents a proposed field change from an untrusted/public source
/// that has not yet been approved by an admin.
/// Stored as part of a JSONB list on the Contact entity.
/// </summary>
public class PendingContactUpdate
{
    /// <summary>
    /// Gets or sets the canonical field name that was proposed for update (e.g. "FirstName", "CompanyName").
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current canonical value at the time the update was proposed.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the new value proposed by the submitter.
    /// </summary>
    public string? ProposedValue { get; set; }

    /// <summary>
    /// Gets or sets the submission source identifier (e.g. "ContactForm", "SubscribeWidget", plugin name).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the IP address of the submitter.
    /// </summary>
    public string? Ip { get; set; }

    /// <summary>
    /// Gets or sets the User-Agent of the submitter.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the update was proposed.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
