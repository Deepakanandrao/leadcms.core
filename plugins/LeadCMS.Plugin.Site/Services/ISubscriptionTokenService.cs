// <copyright file="ISubscriptionTokenService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Service for generating and validating self-contained signed tokens for
/// email subscription confirmation.
/// </summary>
public interface ISubscriptionTokenService
{
    /// <summary>
    /// Generates a signed token encoding the subscription parameters.
    /// </summary>
    /// <param name="email">The subscriber's email address.</param>
    /// <param name="group">The email group to subscribe to.</param>
    /// <param name="language">The subscriber's preferred language.</param>
    /// <param name="timeZoneOffset">The subscriber's timezone offset.</param>
    /// <param name="expiry">Optional custom expiry duration (default 24 hours).</param>
    /// <returns>A URL-safe signed token string.</returns>
    string Generate(string email, string group, string language, int timeZoneOffset, TimeSpan? expiry = null);

    /// <summary>
    /// Validates a signed token and returns its payload if the signature is
    /// valid and the token has not expired.
    /// </summary>
    /// <param name="token">The signed token string.</param>
    /// <returns>The decoded payload, or null if the token is invalid or expired.</returns>
    SubscriptionTokenPayload? Validate(string token);
}
