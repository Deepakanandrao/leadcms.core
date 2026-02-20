// <copyright file="SubscriptionTokenService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LeadCMS.Plugin.Site.Services;

/// <summary>
/// Creates and validates self-contained HMAC-SHA256 signed tokens for email
/// subscription confirmation. The token encodes all subscription parameters
/// (email, group, language, timezone offset, expiry) so no database entity
/// is needed for the confirmation flow.
/// </summary>
public class SubscriptionTokenService : ISubscriptionTokenService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    private readonly byte[] signingKey;

    public SubscriptionTokenService(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException("Signing secret must not be empty.", nameof(secret));
        }

        // Derive a stable 256-bit key from the secret so that any-length string works.
        signingKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    /// <inheritdoc/>
    public string Generate(string email, string group, string language, int timeZoneOffset, TimeSpan? expiry = null)
    {
        var payload = new SubscriptionTokenPayload
        {
            Email = email.ToLowerInvariant(),
            Group = group,
            Language = language,
            TimeZoneOffset = timeZoneOffset,
            ExpiresAtUtc = DateTime.UtcNow.Add(expiry ?? DefaultExpiry),
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        var signature = ComputeSignature(payloadBase64);

        return $"{payloadBase64}.{signature}";
    }

    /// <inheritdoc/>
    public SubscriptionTokenPayload? Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var payloadBase64 = parts[0];
        var providedSignature = parts[1];

        // Verify signature using constant-time comparison
        var expectedSignature = ComputeSignature(payloadBase64);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(providedSignature)))
        {
            return null;
        }

        try
        {
            var payloadBytes = Convert.FromBase64String(payloadBase64);
            var payload = JsonSerializer.Deserialize<SubscriptionTokenPayload>(payloadBytes);

            if (payload == null || payload.ExpiresAtUtc < DateTime.UtcNow)
            {
                return null;
            }

            return payload;
        }
        catch
        {
            return null;
        }
    }

    private string ComputeSignature(string data)
    {
        using var hmac = new HMACSHA256(signingKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}
