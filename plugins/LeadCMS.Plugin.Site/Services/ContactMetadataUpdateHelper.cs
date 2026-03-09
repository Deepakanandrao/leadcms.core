// <copyright file="ContactMetadataUpdateHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Applies locale and tag metadata to a contact.
/// Language and timezone are overwritten; tags are merged additively (deduplicated, case-insensitive).
/// </summary>
public static class ContactMetadataUpdateHelper
{
    public static void ApplyMetadata(
        Contact contact,
        string? language,
        int? timezone,
        IEnumerable<string>? tags)
    {
        if (!string.IsNullOrWhiteSpace(language))
        {
            contact.Language = language;
        }

        if (timezone.HasValue)
        {
            contact.Timezone = timezone.Value;
        }

        MergeTags(contact, tags);
    }

    /// <summary>
    /// Merges incoming tags into a contact's existing tags (additive, deduplicated).
    /// Safe to call from verified flows where ownership has been confirmed.
    /// </summary>
    public static void MergeTags(Contact contact, IEnumerable<string>? tags)
    {
        var normalizedIncomingTags = NormalizeTags(tags);
        if (normalizedIncomingTags.Length == 0)
        {
            return;
        }

        var existingTags = contact.Tags ?? Array.Empty<string>();
        contact.Tags = existingTags
            .Concat(normalizedIncomingTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] NormalizeTags(IEnumerable<string>? tags)
    {
        return tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();
    }
}