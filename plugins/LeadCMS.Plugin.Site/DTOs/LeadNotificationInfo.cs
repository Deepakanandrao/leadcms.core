// <copyright file="LeadNotificationInfo.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Helpers;

namespace LeadCMS.Plugin.Site.DTOs;

/// <summary>
/// Represents information about a captured lead for notification purposes.
/// Contains the actual submitted data so that notifications and email templates
/// always reflect what the user entered, regardless of what is stored in the
/// contact record (which uses an anti-abuse merge policy).
/// </summary>
public class LeadNotificationInfo
{
    /// <summary>
    /// Gets or sets the notification title or topic (e.g., "New demo request", "Contact form submission").
    /// If not provided, a default title will be used based on context.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the template name for internal lead notification emails.
    /// If not provided, the default template is used.
    /// </summary>
    public string? NotificationType { get; set; }

    /// <summary>
    /// Gets or sets the first name of the lead.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name of the lead.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the lead. May be null for phone-only contacts.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number of the lead.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the company name of the lead.
    /// Unified with <c>Contact.CompanyName</c> — both map to the <c>CompanyName</c>
    /// template argument.
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Gets or sets the subject of the inquiry.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the page where the lead was captured.
    /// </summary>
    public string? PageUrl { get; set; }

    /// <summary>
    /// Gets or sets additional data as key-value pairs.
    /// </summary>
    public Dictionary<string, string> ExtraData { get; set; } = new();

    /// <summary>
    /// Gets or sets the language of the lead.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the email attachments.
    /// </summary>
    public List<AttachmentDto>? Attachments { get; set; }

    /// <summary>
    /// Gets or sets the user's time zone offset in minutes (UTC convention).
    /// Unified with <c>Contact.Timezone</c> — both map to the <c>Timezone</c>
    /// template argument.
    /// </summary>
    public int? Timezone { get; set; }

    /// <summary>
    /// Gets or sets the user's IPv4 address.
    /// </summary>
    public string? IpAddressV4 { get; set; }

    /// <summary>
    /// Gets or sets the user's user-agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the contact ID associated with this lead.
    /// </summary>
    public int? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the contact entity so that notification services can build
    /// full template arguments via <see cref="TemplateArgumentsBuilder.FromContact"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Contact? Contact { get; set; }

    /// <summary>
    /// Gets the full name of the lead, or <c>null</c> when neither first nor last name is set.
    /// </summary>
    public string? FullName
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(FirstName))
            {
                parts.Add(FirstName);
            }

            if (!string.IsNullOrWhiteSpace(LastName))
            {
                parts.Add(LastName);
            }

            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }
    }

    /// <summary>
    /// Converts the lead submission data into template arguments using canonical
    /// parameter names that are consistent with <see cref="TemplateArgumentsBuilder.FromContact"/>.
    /// When merged on top of contact-based arguments via
    /// <see cref="TemplateArgumentsBuilder.Merge"/>, the submitted values take precedence
    /// over stale database values, ensuring notifications reflect what the user actually entered.
    /// </summary>
    /// <returns>A case-insensitive dictionary of template arguments.</returns>
    public Dictionary<string, object> ToTemplateArguments()
    {
        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Core contact fields — same canonical names as TemplateArgumentsBuilder.FromContact
        AddIfHasValue(args, "Email", Email);
        AddIfHasValue(args, "FirstName", FirstName);
        AddIfHasValue(args, "LastName", LastName);
        AddIfHasValue(args, "FullName", FullName);
        AddIfHasValue(args, "Phone", Phone);
        AddIfHasValue(args, "CompanyName", CompanyName);
        AddIfHasValue(args, "Language", Language);
        AddIfHasValue(args, "IpAddress", IpAddressV4);
        AddIfHasValue(args, "UserAgent", UserAgent);
        AddIfHasValue(args, "ContactId", ContactId?.ToString());

        if (Timezone.HasValue)
        {
            args["Timezone"] = Timezone.Value.ToString();
            args["TimezoneFormatted"] = TimezoneHelper.FormatUtcOffset(Timezone.Value);
        }

        // Lead-specific fields (not present on Contact)
        AddIfHasValue(args, "Title", Title);
        AddIfHasValue(args, "Subject", Subject);
        AddIfHasValue(args, "Message", Message);
        AddIfHasValue(args, "PageUrl", PageUrl);

        // Extra data entries are added as top-level template arguments
        foreach (var item in ExtraData)
        {
            args[item.Key] = item.Value;
        }

        return args;
    }

    private static void AddIfHasValue(Dictionary<string, object> args, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            args[key] = value;
        }
    }
}
