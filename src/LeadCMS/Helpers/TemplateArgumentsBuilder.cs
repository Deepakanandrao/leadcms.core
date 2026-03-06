// <copyright file="TemplateArgumentsBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

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
        args["Email"] = contact.Email ?? string.Empty;
        args["FirstName"] = contact.FirstName ?? string.Empty;
        args["LastName"] = contact.LastName ?? string.Empty;
        args["FullName"] = contact.FullName
            ?? BuildFullName(contact.FirstName, contact.MiddleName, contact.LastName);
        args["MiddleName"] = contact.MiddleName ?? string.Empty;
        args["Prefix"] = contact.Prefix ?? string.Empty;
        args["Phone"] = contact.Phone ?? string.Empty;
        args["JobTitle"] = contact.JobTitle ?? string.Empty;
        args["CompanyName"] = contact.CompanyName ?? string.Empty;
        args["Department"] = contact.Department ?? string.Empty;
        args["CityName"] = contact.CityName ?? string.Empty;
        args["State"] = contact.State ?? string.Empty;
        args["Zip"] = contact.Zip ?? string.Empty;
        args["Address1"] = contact.Address1 ?? string.Empty;
        args["Address2"] = contact.Address2 ?? string.Empty;
        args["Language"] = contact.Language ?? string.Empty;
        args["CountryCode"] = contact.CountryCode?.ToString() ?? string.Empty;
        args["ContinentCode"] = contact.ContinentCode?.ToString() ?? string.Empty;
        args["Birthday"] = contact.Birthday?.ToString("yyyy-MM-dd") ?? string.Empty;
        args["Timezone"] = contact.Timezone?.ToString() ?? string.Empty;
        args["TimezoneFormatted"] = contact.Timezone.HasValue
            ? TimezoneHelper.FormatUtcOffset(contact.Timezone.Value)
            : string.Empty;
        args["DealsCount"] = contact.DealsCount;
        args["OrdersCount"] = contact.OrdersCount;
        args["LastOrderDate"] = contact.LastOrderDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        args["TotalRevenue"] = contact.TotalRevenue;
        args["Tags"] = contact.Tags?.ToList() ?? new List<string>();
        args["SocialMedia"] = contact.SocialMedia ?? new Dictionary<string, string>();

        // Account fields (flattened for backwards compatibility + nested object)
        args["AccountName"] = contact.Account?.Name ?? string.Empty;
        args["AccountSiteUrl"] = contact.Account?.SiteUrl ?? string.Empty;

        // Domain fields (flattened for backwards compatibility + nested object)
        args["DomainName"] = contact.Domain?.Name ?? string.Empty;

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
        UtmParameters? utmParams)
    {
        if (utmParams == null || !utmParams.HasValues)
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
