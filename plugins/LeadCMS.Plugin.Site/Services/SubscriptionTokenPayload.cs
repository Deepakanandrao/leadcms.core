// <copyright file="SubscriptionTokenPayload.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Represents the decoded payload of a subscription confirmation token.
/// </summary>
public sealed class SubscriptionTokenPayload
{
    /// <summary>
    /// Gets or sets the subscriber's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email group to subscribe to.
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subscriber's preferred language.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subscriber's timezone offset.
    /// </summary>
    public int TimeZoneOffset { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the token expires.
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }
}
