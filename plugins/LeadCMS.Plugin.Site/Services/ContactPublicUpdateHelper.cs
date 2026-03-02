// <copyright file="ContactPublicUpdateHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Centralizes the logic for applying public (untrusted) form submissions
/// to a <see cref="Contact"/> using the fill-only-if-null merge policy.
/// Handles name splitting, phone normalization, and field mapping via
/// <see cref="ContactMergeHelper.ApplyPublicUpdate"/>.
/// </summary>
public static class ContactPublicUpdateHelper
{
    /// <summary>
    /// Applies all one-to-one mappable fields from a public form submission
    /// to the contact using the anti-abuse merge policy.
    /// </summary>
    /// <param name="contact">The contact entity to update.</param>
    /// <param name="firstName">First name from the form.</param>
    /// <param name="middleName">Middle name from the form.</param>
    /// <param name="lastName">Last name from the form.</param>
    /// <param name="company">Company name from the form.</param>
    /// <param name="phone">Phone number from the form.</param>
    /// <param name="sourceName">Source label (e.g. "Contact Us", custom title).</param>
    /// <param name="submissionSource">Submission source identifier (e.g. "ContactForm", "Registration").</param>
    /// <param name="ip">IP address of the submitter.</param>
    /// <param name="userAgent">User-Agent of the submitter.</param>
    /// <param name="phoneNormalizationService">Phone normalization service for E.164 conversion.</param>
    public static void ApplyFormFields(
        Contact contact,
        string? firstName,
        string? middleName,
        string? lastName,
        string? company,
        string? phone,
        string? sourceName,
        string submissionSource,
        string? ip,
        string? userAgent,
        IPhoneNormalizationService phoneNormalizationService)
    {
        ApplyName(contact, firstName, middleName, lastName, submissionSource, ip, userAgent);
        ApplyCompany(contact, company, submissionSource, ip, userAgent);
        ApplyPhone(contact, phone, submissionSource, ip, userAgent, phoneNormalizationService);
        ApplySource(contact, sourceName, submissionSource, ip, userAgent);
    }

    /// <summary>
    /// Applies first, middle and last name values to the contact via the merge
    /// policy. Names are stored as-is without any post-processing.
    /// </summary>
    public static void ApplyName(
        Contact contact,
        string? firstName,
        string? middleName,
        string? lastName,
        string submissionSource,
        string? ip,
        string? userAgent)
    {
        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(contact.FirstName),
            contact.FirstName,
            firstName,
            v => contact.FirstName = v,
            submissionSource,
            ip,
            userAgent);

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(contact.MiddleName),
            contact.MiddleName,
            middleName,
            v => contact.MiddleName = v,
            submissionSource,
            ip,
            userAgent);

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(contact.LastName),
            contact.LastName,
            lastName,
            v => contact.LastName = v,
            submissionSource,
            ip,
            userAgent);
    }

    /// <summary>
    /// Applies company name via the merge policy.
    /// </summary>
    public static void ApplyCompany(
        Contact contact,
        string? company,
        string submissionSource,
        string? ip,
        string? userAgent)
    {
        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(contact.CompanyName),
            contact.CompanyName,
            company,
            v => contact.CompanyName = v,
            submissionSource,
            ip,
            userAgent);
    }

    /// <summary>
    /// Applies phone via the merge policy: always stores original in PhoneRaw,
    /// normalizes into Phone when possible.
    /// </summary>
    public static void ApplyPhone(
        Contact contact,
        string? phone,
        string submissionSource,
        string? ip,
        string? userAgent,
        IPhoneNormalizationService phoneNormalizationService)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        // Always preserve the original user input
        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(contact.PhoneRaw),
            contact.PhoneRaw,
            phone,
            v => contact.PhoneRaw = v,
            submissionSource,
            ip,
            userAgent);

        var normalizedPhone = phoneNormalizationService.Normalize(phone);

        if (normalizedPhone != null)
        {
            ContactMergeHelper.ApplyPublicUpdate(
                contact,
                nameof(contact.Phone),
                contact.Phone,
                normalizedPhone,
                v => contact.Phone = v,
                submissionSource,
                ip,
                userAgent);
        }
    }

    /// <summary>
    /// Applies source label via the merge policy.
    /// </summary>
    public static void ApplySource(
        Contact contact,
        string? sourceName,
        string submissionSource,
        string? ip,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return;
        }

        ContactMergeHelper.ApplyPublicUpdate(
            contact,
            nameof(contact.Source),
            contact.Source,
            sourceName,
            v => contact.Source = v,
            submissionSource,
            ip,
            userAgent);
    }
}
