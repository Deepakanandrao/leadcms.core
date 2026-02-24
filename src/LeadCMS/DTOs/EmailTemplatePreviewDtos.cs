// <copyright file="EmailTemplatePreviewDtos.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LeadCMS.Enums;

namespace LeadCMS.DTOs;

public class EmailTemplatePreviewRequestDto
{
    /// <summary>
    /// Gets or sets the subject line template (may contain Liquid variables).
    /// </summary>
    [Required]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body template (may contain Liquid variables).
    /// </summary>
    [Required]
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender display name.
    /// </summary>
    [Required]
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the language for template matching. Defaults to system default language if not provided.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the specific contact ID to use for rendering the template preview.
    /// When not provided, a dummy contact with meaningful sample data is generated.
    /// </summary>
    public int? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the type of dummy contact to generate for the preview.
    /// Ignored when <see cref="ContactId"/> is provided. Defaults to <see cref="PreviewContactType.Full"/>.
    /// </summary>
    public PreviewContactType? ContactType { get; set; }

    /// <summary>
    /// Gets or sets custom template parameters provided by client code.
    /// These values are merged on top of built-in contact template arguments.
    /// </summary>
    public Dictionary<string, JsonElement>? CustomTemplateParameters { get; set; }
}

public class EmailTemplatePreviewResultDto
{
    /// <summary>
    /// Gets or sets the rendered subject line with template variables replaced.
    /// </summary>
    public string RenderedSubject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rendered HTML body with template variables replaced.
    /// </summary>
    public string RenderedBody { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender email address from the matched template.
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender name from the matched template.
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ID of the contact used for rendering the preview.
    /// Zero when a dummy contact was generated.
    /// </summary>
    public int PreviewContactId { get; set; }

    /// <summary>
    /// Gets or sets the name of the contact used for rendering the preview.
    /// </summary>
    public string PreviewContactName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email of the contact used for rendering the preview.
    /// </summary>
    public string PreviewContactEmail { get; set; } = string.Empty;
}

public class EmailTemplateSendTestDto
{
    /// <summary>
    /// Gets or sets the subject line template (may contain Liquid variables).
    /// </summary>
    [Required]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body template (may contain Liquid variables).
    /// </summary>
    [Required]
    public string BodyTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender email address.
    /// </summary>
    [Required]
    [EmailAddress]
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender display name.
    /// </summary>
    [Required]
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the contact ID whose data will be used to render the template.
    /// When not provided, a dummy contact is used.
    /// </summary>
    public int? ContactId { get; set; }

    /// <summary>
    /// Gets or sets the type of dummy contact to generate when no ContactId is provided.
    /// Ignored when <see cref="ContactId"/> is provided. Defaults to <see cref="PreviewContactType.Full"/>.
    /// </summary>
    public PreviewContactType? ContactType { get; set; }

    /// <summary>
    /// Gets or sets custom template parameters provided by client code.
    /// These values are merged on top of built-in contact template arguments.
    /// </summary>
    public Dictionary<string, JsonElement>? CustomTemplateParameters { get; set; }

    /// <summary>
    /// Gets or sets the email address to deliver the test email to.
    /// </summary>
    [Required]
    [EmailAddress]
    public string RecipientEmail { get; set; } = string.Empty;
}
