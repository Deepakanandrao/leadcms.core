// <copyright file="EmailTemplateFormat.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enums;

/// <summary>
/// Defines the body template format for email templates.
/// </summary>
public enum EmailTemplateFormat
{
    /// <summary>
    /// Raw HTML format (legacy default for all existing templates).
    /// </summary>
    Html = 0,

    /// <summary>
    /// MJML format — compiled to HTML before sending. Supports MJML templating
    /// features such as conditions, loops, and structured components.
    /// </summary>
    Mjml = 1,
}
