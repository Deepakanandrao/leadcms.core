// <copyright file="PreviewContactType.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enums;

/// <summary>
/// Defines the level of detail for the dummy contact used in email template previews.
/// Ignored when a specific <c>ContactId</c> is provided.
/// </summary>
public enum PreviewContactType
{
    /// <summary>
    /// Fully populated dummy contact including all nested objects
    /// (Account, Domain, Orders with OrderItems, Deals with Pipeline/Stage).
    /// </summary>
    Full = 0,

    /// <summary>
    /// Top-level contact fields only (name, email, phone, address, etc.)
    /// without any nested objects.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Minimal dummy contact with email, first name, and last name only.
    /// </summary>
    Basic = 2,

    /// <summary>
    /// Bare-minimum dummy contact with only an email address.
    /// </summary>
    Minimal = 3,
}
