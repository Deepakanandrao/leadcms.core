// <copyright file="TemplateArgumentsBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Configuration;
using LeadCMS.Entities;
using LeadCMS.Models;

namespace LeadCMS.Helpers;

/// <summary>
/// Builds template arguments dictionaries for use with the Liquid template engine.
/// Provides a unified way to construct template variables from domain entities
/// and allows callers to merge custom arguments on top.
/// </summary>
public static class TemplateArgumentsBuilder
{
    /// <summary>
    /// Builds template arguments from a <see cref="Contact"/> entity, including
    /// related Account, Domain, Orders, and Deals when loaded.
    /// </summary>
    /// <param name="contact">The contact to extract template values from, or <c>null</c>.</param>
    /// <param name="includeNestedObjects">When <c>true</c> (the default), nested objects
    /// (Account, Domain, Orders, Deals) are included as template variables.
    /// When <c>false</c>, only flattened scalar fields (e.g. AccountName, DomainName) are emitted.</param>
    /// <returns>A dictionary of template arguments with string keys and object values.
    /// String values are provided for simple fields; collections and complex objects
    /// are passed as-is so the Liquid engine can iterate and access their properties.</returns>
    public static Dictionary<string, object> FromContact(Contact? contact, bool includeNestedObjects = true)
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (contact == null)
        {
            return args;
        }

        // Scalar contact fields
        AddIfHasValue(args, "Email", contact.Email);
        AddIfHasValue(args, "FirstName", contact.FirstName);
        AddIfHasValue(args, "LastName", contact.LastName);
        AddIfHasValue(
            args,
            "FullName",
            contact.FullName ?? BuildFullName(contact.FirstName, contact.MiddleName, contact.LastName));
        AddIfHasValue(args, "MiddleName", contact.MiddleName);
        AddIfHasValue(args, "Prefix", contact.Prefix);
        AddIfHasValue(args, "Phone", contact.Phone ?? contact.PhoneRaw);
        AddIfHasValue(args, "JobTitle", contact.JobTitle);
        AddIfHasValue(args, "CompanyName", contact.CompanyName);
        AddIfHasValue(args, "Department", contact.Department);
        AddIfHasValue(args, "CityName", contact.CityName);
        AddIfHasValue(args, "State", contact.State);
        AddIfHasValue(args, "Zip", contact.Zip);
        AddIfHasValue(args, "Address1", contact.Address1);
        AddIfHasValue(args, "Address2", contact.Address2);
        AddIfHasValue(args, "Language", contact.Language);
        AddIfHasValue(args, "CountryCode", contact.CountryCode?.ToString());
        AddIfHasValue(args, "ContinentCode", contact.ContinentCode?.ToString());
        var formattedTimezone = contact.Timezone.HasValue
            ? TimezoneHelper.FormatUtcOffset(contact.Timezone.Value)
            : null;

        AddIfHasValue(args, "Birthday", contact.Birthday?.ToString("yyyy-MM-dd"));
        AddIfHasValue(args, "Timezone", contact.Timezone?.ToString());
        AddIfHasValue(args, "TimezoneFormatted", formattedTimezone);
        AddIfHasValue(args, "IpAddress", contact.UpdatedByIp ?? contact.CreatedByIp);
        AddIfHasValue(
            args,
            "UserDeviceSummary",
            UserAgentDeviceSummaryHelper.Parse(contact.UpdatedByUserAgent ?? contact.CreatedByUserAgent));
        args["DealsCount"] = contact.DealsCount;
        args["OrdersCount"] = contact.OrdersCount;
        AddIfHasValue(args, "LastOrderDate", contact.LastOrderDate?.ToString("yyyy-MM-dd"));
        args["TotalRevenue"] = contact.TotalRevenue;
        args["Tags"] = contact.Tags?.ToList() ?? new List<string>();
        args["SocialMedia"] = contact.SocialMedia ?? new Dictionary<string, string>();

        // UTM acquisition parameters (first-touch attribution)
        if (contact.Utms != null && contact.Utms.HasValues())
        {
            AddIfHasValue(args, "contact_utm_source", contact.Utms.Source);
            AddIfHasValue(args, "contact_utm_medium", contact.Utms.Medium);
            AddIfHasValue(args, "contact_utm_campaign", contact.Utms.Campaign);
            AddIfHasValue(args, "contact_utm_content", contact.Utms.Content);
            AddIfHasValue(args, "contact_utm_term", contact.Utms.Term);
            AddIfHasValue(args, "contact_utm_id", contact.Utms.Id);
        }

        // Account fields (flattened for backwards compatibility + nested object)
        AddIfHasValue(args, "AccountName", contact.Account?.Name);
        AddIfHasValue(args, "AccountSiteUrl", contact.Account?.SiteUrl);

        // Domain fields (flattened for backwards compatibility + nested object)
        AddIfHasValue(args, "DomainName", contact.Domain?.Name);

        if (includeNestedObjects)
        {
            if (contact.Account != null)
            {
                args["Account"] = contact.Account;
            }

            if (contact.Domain != null)
            {
                args["Domain"] = contact.Domain;
            }

            // Collections — sorted newest-first (by UpdatedAt ?? CreatedAt) so that
            // Liquid templates can access the most recent item first, e.g. {% for order in Orders limit:1 %}
            if (contact.Orders != null)
            {
                args["Orders"] = contact.Orders
                    .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
                    .ToList();
            }

            if (contact.Deals != null)
            {
                args["Deals"] = contact.Deals
                    .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                    .ToList();
            }
        }

        return args;
    }

    /// <summary>
    /// Merges additional custom arguments into an existing arguments dictionary.
    /// Custom arguments take precedence over existing values with the same key.
    /// </summary>
    /// <param name="baseArgs">The base arguments dictionary to merge into.</param>
    /// <param name="customArgs">Additional arguments to add or override.</param>
    /// <returns>The same <paramref name="baseArgs"/> dictionary with merged values, for fluent chaining.</returns>
    public static Dictionary<string, object> Merge(
        Dictionary<string, object> baseArgs,
        Dictionary<string, object>? customArgs)
    {
        if (customArgs == null)
        {
            return baseArgs;
        }

        foreach (var kv in customArgs)
        {
            baseArgs[kv.Key] = kv.Value;
        }

        return baseArgs;
    }

    /// <summary>
    /// Merges UTM parameters into an existing template arguments dictionary.
    /// UTM keys (<c>utm_source</c>, <c>utm_medium</c>, …) and the pre-built <c>utm_query</c>
    /// are added so that email templates can reference them directly:
    /// <code>&lt;a href="https://example.com/pricing?{{ utm_query }}"&gt;Click here&lt;/a&gt;</code>
    /// </summary>
    /// <param name="args">The template arguments dictionary to merge into.</param>
    /// <param name="utmParams">UTM parameters to add. When <c>null</c> or empty, the dictionary is returned unchanged.</param>
    /// <returns>The same <paramref name="args"/> dictionary with UTM values merged in, for fluent chaining.</returns>
    public static Dictionary<string, object> WithUtmParameters(
        Dictionary<string, object> args,
        Utms? utmParams)
    {
        if (utmParams == null || !utmParams.HasValues())
        {
            return args;
        }

        foreach (var kv in utmParams.ToDictionary())
        {
            args[kv.Key] = kv.Value;
        }

        return args;
    }

    /// <summary>
    /// Merges site link URLs from application settings into an existing template arguments dictionary.
    /// Only non-empty values are added. The following variables become available in Liquid templates:
    /// <c>{{ site_url }}</c>, <c>{{ unsubscribe_url }}</c>, <c>{{ privacy_url }}</c>.
    /// </summary>
    /// <param name="args">The template arguments dictionary to merge into.</param>
    /// <param name="siteLinks">Site link configuration from application settings.</param>
    /// <returns>The same <paramref name="args"/> dictionary with site link values merged in, for fluent chaining.</returns>
    public static Dictionary<string, object> WithSiteLinks(
        Dictionary<string, object> args,
        SiteLinksConfig? siteLinks)
    {
        if (siteLinks == null)
        {
            return args;
        }

        if (!string.IsNullOrWhiteSpace(siteLinks.SiteUrl))
        {
            args["site_url"] = siteLinks.SiteUrl.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(siteLinks.UnsubscribeUrl))
        {
            args["unsubscribe_url"] = siteLinks.UnsubscribeUrl.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(siteLinks.PrivacyUrl))
        {
            args["privacy_url"] = siteLinks.PrivacyUrl.TrimEnd('/');
        }

        return args;
    }

    /// <summary>
    /// Merges email history parameters into an existing template arguments dictionary.
    /// Adds the last sent and last received email details for the contact so templates can
    /// reference them, e.g. <c>{{ LastSentEmailTitle }}</c> or <c>{{ LastReceivedEmailFromName }}</c>.
    /// </summary>
    /// <param name="args">The template arguments dictionary to merge into.</param>
    /// <param name="lastSentEmail">The most recent <see cref="EmailLog"/> with <see cref="EmailStatus.Sent"/> for the contact, or <c>null</c>.</param>
    /// <param name="lastReceivedEmail">The most recent <see cref="EmailLog"/> with <see cref="EmailStatus.Received"/> for the contact, or <c>null</c>.</param>
    /// <returns>The same <paramref name="args"/> dictionary with email history values merged in, for fluent chaining.</returns>
    public static Dictionary<string, object> WithEmailHistory(
        Dictionary<string, object> args,
        EmailLog? lastSentEmail,
        EmailLog? lastReceivedEmail)
    {
        if (lastSentEmail != null)
        {
            args["LastSentEmailDate"] = lastSentEmail.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            AddIfHasValue(args, "LastSentEmailTitle", lastSentEmail.Subject);
            AddIfHasValue(args, "LastSentEmailBody", lastSentEmail.HtmlBody);
            AddIfHasValue(args, "LastSentEmailFromName", lastSentEmail.FromName);
            AddIfHasValue(args, "LastSentEmailFromEmail", lastSentEmail.FromEmail);
        }

        if (lastReceivedEmail != null)
        {
            args["LastReceivedEmailDate"] = lastReceivedEmail.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            AddIfHasValue(args, "LastReceivedEmailTitle", lastReceivedEmail.Subject);
            AddIfHasValue(args, "LastReceivedEmailBody", lastReceivedEmail.HtmlBody);
            AddIfHasValue(args, "LastReceivedEmailFromName", lastReceivedEmail.FromName);
            AddIfHasValue(args, "LastReceivedEmailFromEmail", lastReceivedEmail.FromEmail);
        }

        return args;
    }

    /// <summary>
    /// Overlays pending update values from a contact onto the template arguments dictionary.
    /// Pending values take priority over the contact's current values so that acknowledgment
    /// emails reflect what the submitter actually typed, not the stored (possibly different) data.
    /// Only the latest pending value per field is used (the list is guaranteed to have at most one per field).
    /// </summary>
    /// <param name="args">The template arguments dictionary to overlay into.</param>
    /// <param name="contact">The contact whose <see cref="Contact.PendingUpdates"/> to read.</param>
    /// <returns>The same <paramref name="args"/> dictionary with pending values merged in, for fluent chaining.</returns>
    public static Dictionary<string, object> WithPendingUpdates(
        Dictionary<string, object> args,
        Contact? contact)
    {
        if (contact?.PendingUpdates == null || contact.PendingUpdates.Count == 0)
        {
            return args;
        }

        foreach (var pending in contact.PendingUpdates)
        {
            var key = MapPendingFieldToTemplateKey(pending.Field);
            if (key != null && !string.IsNullOrWhiteSpace(pending.ProposedValue))
            {
                args[key] = pending.ProposedValue;
            }
        }

        // Rebuild FullName if any name part was overridden by a pending update
        var hasNamePending = contact.PendingUpdates.Exists(p =>
            p.Field is nameof(Contact.FirstName)
                    or nameof(Contact.MiddleName)
                    or nameof(Contact.LastName));

        if (hasNamePending)
        {
            var firstName = args.TryGetValue("FirstName", out var fn) ? fn?.ToString() : null;
            var middleName = args.TryGetValue("MiddleName", out var mn) ? mn?.ToString() : null;
            var lastName = args.TryGetValue("LastName", out var ln) ? ln?.ToString() : null;
            var fullName = BuildFullName(firstName, middleName, lastName);
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                args["FullName"] = fullName;
            }
        }

        return args;
    }

    /// <summary>
    /// Maps a <see cref="PendingContactUpdate.Field"/> name to the corresponding template argument key.
    /// Returns <c>null</c> for fields that don't have a direct template mapping.
    /// </summary>
    private static string? MapPendingFieldToTemplateKey(string fieldName)
    {
        return fieldName switch
        {
            nameof(Contact.FirstName) => "FirstName",
            nameof(Contact.MiddleName) => "MiddleName",
            nameof(Contact.LastName) => "LastName",
            nameof(Contact.CompanyName) => "CompanyName",
            nameof(Contact.Phone) => "Phone",
            nameof(Contact.PhoneRaw) => "Phone",      // PhoneRaw also maps to Phone template key
            nameof(Contact.Source) => "Source",
            _ => null,
        };
    }

    private static void AddIfHasValue(Dictionary<string, object> args, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            args[key] = value;
        }
    }

    /// <summary>
    /// Computes a full name from constituent parts, mirroring the database computed column logic.
    /// Used when <see cref="Contact.FullName"/> is <c>null</c> (e.g. for in-memory dummy contacts).
    /// </summary>
    private static string BuildFullName(string? firstName, string? middleName, string? lastName)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(firstName))
        {
            parts.Add(firstName);
        }

        if (!string.IsNullOrEmpty(middleName))
        {
            parts.Add(middleName);
        }

        if (!string.IsNullOrEmpty(lastName))
        {
            parts.Add(lastName);
        }

        return string.Join(" ", parts);
    }
}
