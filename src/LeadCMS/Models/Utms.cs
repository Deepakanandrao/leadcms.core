// <copyright file="Utms.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text;
using System.Web;

namespace LeadCMS.Models;

/// <summary>
/// Represents standard UTM (Urchin Tracking Module) parameters used to tag URLs for
/// marketing attribution in analytics tools such as Google Analytics.
///
/// <para>
/// Supports three layers of precedence (lowest → highest):
/// <list type="number">
///   <item><description>Defaults — hardcoded baseline (e.g. <c>utm_source=leadcms</c>, <c>utm_medium=email</c>).</description></item>
///   <item><description>Context — set by business-logic context such as campaign name or template name.</description></item>
///   <item><description>Overrides — explicit values from user/plugin/API input that take highest priority.</description></item>
/// </list>
/// Merging is handled by <see cref="LeadCMS.Helpers.UtmsBuilder"/>.
/// </para>
/// </summary>
public class Utms
{
    /// <summary>
    /// Gets or sets <c>utm_source</c> — identifies where the traffic comes from
    /// (e.g. <c>leadcms</c>, <c>sendgrid</c>, <c>newsletter</c>).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets <c>utm_medium</c> — the marketing channel type
    /// (e.g. <c>email</c>, <c>cpc</c>, <c>social</c>).
    /// </summary>
    public string? Medium { get; set; }

    /// <summary>
    /// Gets or sets <c>utm_campaign</c> — the campaign name or identifier
    /// (e.g. <c>spring_sale_2026</c>, <c>onboarding_day_3</c>).
    /// </summary>
    public string? Campaign { get; set; }

    /// <summary>
    /// Gets or sets <c>utm_content</c> — variant or placement identifier,
    /// useful for A/B testing or distinguishing multiple links in one email
    /// (e.g. <c>cta_top</c>, <c>hero_button</c>).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets <c>utm_term</c> — originally for paid search keywords,
    /// sometimes reused for internal marketing classification.
    /// </summary>
    public string? Term { get; set; }

    /// <summary>
    /// Gets or sets <c>utm_id</c> — campaign identifier recognised by Google Analytics.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Returns whether at least one UTM parameter has a non-empty value.
    /// </summary>
    public bool HasValues() =>
        !string.IsNullOrWhiteSpace(Source) ||
        !string.IsNullOrWhiteSpace(Medium) ||
        !string.IsNullOrWhiteSpace(Campaign) ||
        !string.IsNullOrWhiteSpace(Content) ||
        !string.IsNullOrWhiteSpace(Term) ||
        !string.IsNullOrWhiteSpace(Id);

    /// <summary>
    /// Converts the populated UTM parameters into a flat dictionary suitable for
    /// injection into email template variables. Keys follow the standard
    /// <c>utm_*</c> naming convention. Empty/null values are omitted.
    ///
    /// <para>Additionally, a composite key <c>utm_query</c> is included that contains
    /// the pre-built query-string fragment (e.g. <c>utm_source=leadcms&amp;utm_medium=email&amp;…</c>)
    /// so templates can append it to URLs directly.</para>
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(Source))
        {
            dict["utm_source"] = Source;
        }

        if (!string.IsNullOrWhiteSpace(Medium))
        {
            dict["utm_medium"] = Medium;
        }

        if (!string.IsNullOrWhiteSpace(Campaign))
        {
            dict["utm_campaign"] = Campaign;
        }

        if (!string.IsNullOrWhiteSpace(Content))
        {
            dict["utm_content"] = Content;
        }

        if (!string.IsNullOrWhiteSpace(Term))
        {
            dict["utm_term"] = Term;
        }

        if (!string.IsNullOrWhiteSpace(Id))
        {
            dict["utm_id"] = Id;
        }

        var queryString = ToQueryString();
        if (!string.IsNullOrEmpty(queryString))
        {
            dict["utm_query"] = queryString;
        }

        return dict;
    }

    /// <summary>
    /// Builds a URL query-string fragment from the populated UTM parameters.
    /// Values are URL-encoded. Returns an empty string when no parameters are set.
    /// </summary>
    /// <example>
    /// <code>utm_source=leadcms&amp;utm_medium=email&amp;utm_campaign=onboarding_day_3</code>
    /// </example>
    public string ToQueryString()
    {
        var pairs = new List<string>();

        if (!string.IsNullOrWhiteSpace(Source))
        {
            pairs.Add($"utm_source={HttpUtility.UrlEncode(Source)}");
        }

        if (!string.IsNullOrWhiteSpace(Medium))
        {
            pairs.Add($"utm_medium={HttpUtility.UrlEncode(Medium)}");
        }

        if (!string.IsNullOrWhiteSpace(Campaign))
        {
            pairs.Add($"utm_campaign={HttpUtility.UrlEncode(Campaign)}");
        }

        if (!string.IsNullOrWhiteSpace(Content))
        {
            pairs.Add($"utm_content={HttpUtility.UrlEncode(Content)}");
        }

        if (!string.IsNullOrWhiteSpace(Term))
        {
            pairs.Add($"utm_term={HttpUtility.UrlEncode(Term)}");
        }

        if (!string.IsNullOrWhiteSpace(Id))
        {
            pairs.Add($"utm_id={HttpUtility.UrlEncode(Id)}");
        }

        return string.Join("&", pairs);
    }
}
