// <copyright file="ContactMergeHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Helpers;

/// <summary>
/// Implements the "fill only if null" anti-abuse merge policy for contact updates
/// from untrusted/public sources. When a field already has a value and the proposed
/// value differs, the change is recorded in PendingUpdates for admin review
/// instead of being applied directly.
/// </summary>
public static class ContactMergeHelper
{
    /// <summary>
    /// Applies a proposed field value to a contact using the fill-only-if-null policy.
    /// If the field is currently null/empty, the value is applied directly.
    /// If the field already has a different value, the change is stored in PendingUpdates.
    /// </summary>
    /// <param name="contact">The contact to update.</param>
    /// <param name="fieldName">The canonical field name (e.g. "FirstName").</param>
    /// <param name="currentValue">The current value of the field on the contact.</param>
    /// <param name="proposedValue">The proposed new value from the public source.</param>
    /// <param name="setter">Action to set the field value on the contact if accepted.</param>
    /// <param name="source">Submission source identifier (e.g. "ContactForm").</param>
    /// <param name="ip">IP address of the submitter.</param>
    /// <param name="userAgent">User-Agent of the submitter.</param>
    public static void ApplyPublicUpdate(
        Contact contact,
        string fieldName,
        string? currentValue,
        string? proposedValue,
        Action<string> setter,
        string source,
        string? ip,
        string? userAgent)
    {
        // Nothing to apply
        if (string.IsNullOrWhiteSpace(proposedValue))
        {
            return;
        }

        // Field is empty — fill directly
        if (string.IsNullOrWhiteSpace(currentValue))
        {
            setter(proposedValue);
            return;
        }

        // Same value — no conflict
        if (string.Equals(currentValue, proposedValue, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Conflict — store in PendingUpdates for admin review
        contact.PendingUpdates ??= new List<PendingContactUpdate>();

        contact.PendingUpdates.Add(new PendingContactUpdate
        {
            Field = fieldName,
            OldValue = currentValue,
            ProposedValue = proposedValue,
            Source = source,
            Ip = ip,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
