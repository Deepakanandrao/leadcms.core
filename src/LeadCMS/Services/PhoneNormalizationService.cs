// <copyright file="PhoneNormalizationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Geography;
using LeadCMS.Helpers;
using LeadCMS.Interfaces;
using PhoneNumbers;

namespace LeadCMS.Services;

/// <summary>
/// Normalizes raw phone input to E.164 format using a multi-strategy approach.
/// Strategies are tried in order: as-is, contact country, language/locale hint, default region.
/// </summary>
public class PhoneNormalizationService : IPhoneNormalizationService
{
    private const string HardFallbackRegion = "US";
    private static readonly PhoneNumberUtil PhoneUtil = PhoneNumberUtil.GetInstance();
    private readonly string defaultRegion;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNormalizationService"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration used to resolve the default language/region.</param>
    public PhoneNormalizationService(IConfiguration configuration)
    {
        var defaultLanguage = LanguageHelper.GetDefaultLanguage(configuration);
        defaultRegion = ExtractRegionFromLanguage(defaultLanguage) ?? HardFallbackRegion;
    }

    /// <inheritdoc/>
    public string? Normalize(string? rawPhone, Country? countryCode = null, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            return null;
        }

        var stripped = rawPhone.Trim();

        // Strategy 1: Try parsing as-is (works for inputs with + prefix or full international format)
        var result = TryParse(stripped, null);
        if (result != null)
        {
            return result;
        }

        // Strategy 2: Use the contact's country code as the region hint
        if (countryCode.HasValue && countryCode.Value != Country.ZZ)
        {
            var region = countryCode.Value.ToString();
            result = TryParse(stripped, region);
            if (result != null)
            {
                return result;
            }
        }

        // Strategy 3: Derive region from language/locale (e.g. "en-US" → "US", "de-DE" → "DE")
        var langRegion = ExtractRegionFromLanguage(language);
        if (langRegion != null)
        {
            result = TryParse(stripped, langRegion);
            if (result != null)
            {
                return result;
            }
        }

        // Strategy 4: Fall back to default region (derived from SupportedLanguages config)
        result = TryParse(stripped, defaultRegion);
        if (result != null)
        {
            return result;
        }

        // All strategies failed — return null so the caller can store in PhoneRaw
        return null;
    }

    private static string? TryParse(string phone, string? region)
    {
        try
        {
            var parsed = PhoneUtil.Parse(phone, region);

            if (PhoneUtil.IsValidNumber(parsed))
            {
                return PhoneUtil.Format(parsed, PhoneNumberFormat.E164);
            }
        }
        catch (NumberParseException)
        {
            // Not parseable with this region — fall through
        }

        return null;
    }

    private static string? ExtractRegionFromLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        // Handle BCP-47 locale tags: "en-US" → "US", "pt-BR" → "BR"
        var parts = language.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var candidate = parts[^1].ToUpperInvariant();

            // Validate it's a known region for libphonenumber
            if (candidate.Length == 2 && PhoneUtil.GetSupportedRegions().Contains(candidate))
            {
                return candidate;
            }
        }

        // Single language code without region (e.g. "de") — try mapping to primary region
        var langCode = parts[0].ToLowerInvariant();
        if (langCode.Length == 2)
        {
            var region = PhoneUtil.GetRegionCodeForCountryCode(
                PhoneUtil.GetCountryCodeForRegion(langCode.ToUpperInvariant()));

            if (region != null && region != "ZZ")
            {
                return region;
            }
        }

        return null;
    }
}
