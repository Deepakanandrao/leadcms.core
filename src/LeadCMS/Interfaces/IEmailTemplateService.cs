// <copyright file="IEmailTemplateService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.DTOs;

namespace LeadCMS.Interfaces;

public interface IEmailTemplateService
{
    /// <summary>
    /// Generates an email template preview by rendering the supplied template with a real contact's data
    /// or with a dummy contact containing meaningful sample data when no contact ID is provided.
    /// The template does not need to be persisted — the caller supplies subject, body, format, and sender info inline.
    /// </summary>
    /// <param name="dto">The preview request containing inline template data, optional contact ID, and optional custom variables.</param>
    /// <returns>The preview result with rendered HTML, subject, sender info, and preview contact details.</returns>
    Task<EmailTemplatePreviewResultDto> PreviewAsync(EmailTemplatePreviewRequestDto dto);

    /// <summary>
    /// Sends a test email using a specific contact's data but delivered to a different email address.
    /// Does not require a saved campaign — works with just a template and contact.
    /// </summary>
    /// <param name="dto">The test email request containing template ID, contact ID, and target email.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendTestEmailAsync(EmailTemplateSendTestDto dto);
}
