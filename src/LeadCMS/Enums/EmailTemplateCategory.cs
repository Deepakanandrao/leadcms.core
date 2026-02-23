// <copyright file="EmailTemplateCategory.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Enums;

/// <summary>
/// Categorises email templates by their visual style and intended purpose.
/// Used to provide AI with additional context when generating or editing templates.
/// </summary>
public enum EmailTemplateCategory
{
    /// <summary>
    /// General-purpose template with no specific category constraints.
    /// This is the default for all existing templates.
    /// </summary>
    General = 0,

    /// <summary>
    /// Plain-text / personal-style — minimal formatting, 1:1 human tone, looks like a normal personal email.
    /// </summary>
    PlainText = 1,

    /// <summary>
    /// Simple professional — clean layout, logo/header, short sections, subtle CTA;
    /// common for SaaS and product updates.
    /// </summary>
    SimpleProfessional = 2,

    /// <summary>
    /// Newsletter / editorial — content blocks, headlines, images, "read more" links;
    /// suited for media and content-heavy brands.
    /// </summary>
    Newsletter = 3,

    /// <summary>
    /// Promotional / marketing — strong hero, discount/offer-first, bold CTAs, urgency elements.
    /// </summary>
    Promotional = 4,

    /// <summary>
    /// Transactional — receipts, confirmations, password resets, shipping updates;
    /// utility-first and highly scannable.
    /// </summary>
    Transactional = 5,

    /// <summary>
    /// Lifecycle / drip — onboarding, activation, re-engagement;
    /// educational sequence with progressive CTAs.
    /// </summary>
    Lifecycle = 6,

    /// <summary>
    /// Digest / report — KPI summaries, charts/tables, periodic updates;
    /// commonly used by B2B and analytics tools.
    /// </summary>
    Digest = 7,

    /// <summary>
    /// Event / invitation — date/time prominent, RSVP button, agenda/speaker cards.
    /// </summary>
    Event = 8,

    /// <summary>
    /// Alert / notification — compact, priority/status colour cues, action-required emphasis.
    /// </summary>
    Alert = 9,
}
