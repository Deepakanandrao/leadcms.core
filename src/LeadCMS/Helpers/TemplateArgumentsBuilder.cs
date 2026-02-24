// <copyright file="TemplateArgumentsBuilder.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;

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
