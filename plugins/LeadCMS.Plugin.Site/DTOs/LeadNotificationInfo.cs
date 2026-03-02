// <copyright file="LeadNotificationInfo.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Plugin.Site.DTOs;

/// <summary>
/// Represents information about a captured lead for notification purposes.
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
    /// </summary>
    public string? Company { get; set; }

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
    /// Gets or sets the user's time zone offset in minutes.
    /// </summary>
    public int? TimeZoneOffset { get; set; }

    /// <summary>
    /// Gets or sets the user's IP address.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the user's user-agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the contact ID associated with this lead.
    /// </summary>
    public int? ContactId { get; set; }

    /// <summary>
    /// Gets the full name of the lead.
    /// </summary>
    public string FullName
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

            return parts.Count > 0 ? string.Join(" ", parts) : "-";
        }
    }
}
