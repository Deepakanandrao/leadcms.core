// <copyright file="CurrencyInfoHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Globalization;
using LeadCMS.DTOs;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Helpers;

public static class CurrencyInfoHelper
{
    private static readonly Lazy<List<CurrencyInfoDto>> CachedCurrencies = new Lazy<List<CurrencyInfoDto>>(BuildCurrencies);

    public static List<CurrencyInfoDto> GetAll()
    {
        return CachedCurrencies.Value
            .OrderBy(dto => dto.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static CurrencyInfoDto? GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return CachedCurrencies.Value
            .FirstOrDefault(dto => string.Equals(dto.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public static CurrencyInfoDto? GetPrimaryCurrencyInfo(IConfiguration configuration)
    {
        var primaryCurrencyCode = GetPrimaryCurrencyCode(configuration);
        var supportedLanguages = LanguageHelper.GetSupportedLanguages(configuration);

        var defaultCulture = TryGetSpecificCulture(supportedLanguages[0]);
        if (defaultCulture != null)
        {
            var region = new RegionInfo(defaultCulture.Name);
            if (string.Equals(region.ISOCurrencySymbol, primaryCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return BuildCurrencyInfo(region, defaultCulture.NumberFormat, defaultCulture.Name);
            }
        }

        return GetByCode(primaryCurrencyCode);
    }

    public static string? GetDefaultCurrencyCode(IEnumerable<string> supportedLanguages)
    {
        var language = supportedLanguages.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            if (culture.IsNeutralCulture)
            {
                culture = CultureInfo.CreateSpecificCulture(language);
            }

            var region = new RegionInfo(culture.Name);
            return string.IsNullOrWhiteSpace(region.ISOCurrencySymbol) ? null : region.ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public static string GetPrimaryCurrencyCode(IConfiguration configuration)
    {
        var settingValue = configuration["Currency:Primary"];
        if (!string.IsNullOrWhiteSpace(settingValue))
        {
            return settingValue.Trim();
        }

        var supportedLanguages = LanguageHelper.GetSupportedLanguages(configuration);

        return GetDefaultCurrencyCode(supportedLanguages) ?? "USD";
    }

    private static CultureInfo? TryGetSpecificCulture(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(language);
            return culture.IsNeutralCulture ? CultureInfo.CreateSpecificCulture(language) : culture;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsCultureEligible(CultureInfo culture)
    {
        if (culture.CultureTypes.HasFlag(CultureTypes.UserCustomCulture)
            || culture.CultureTypes.HasFlag(CultureTypes.ReplacementCultures))
        {
            return false;
        }

        return !culture.Name.StartsWith("x-", StringComparison.OrdinalIgnoreCase)
            && !culture.Name.StartsWith("qps-", StringComparison.OrdinalIgnoreCase)
            && !culture.Name.StartsWith("apw-", StringComparison.OrdinalIgnoreCase);
    }

    private static CurrencyInfoDto BuildCurrencyInfo(RegionInfo region, NumberFormatInfo format, string cultureName)
    {
        return new CurrencyInfoDto
        {
            Code = region.ISOCurrencySymbol,
            EnglishName = region.CurrencyEnglishName,
            NativeName = region.CurrencyNativeName,
            Symbol = region.CurrencySymbol,
            DecimalDigits = format.CurrencyDecimalDigits,
            DecimalSeparator = format.CurrencyDecimalSeparator,
            GroupSeparator = format.CurrencyGroupSeparator,
            PositivePattern = format.CurrencyPositivePattern,
            NegativePattern = format.CurrencyNegativePattern,
            CultureName = cultureName,
        };
    }

    private static List<CurrencyInfoDto> BuildCurrencies()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        var byCode = new Dictionary<string, CurrencyInfoDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in cultures)
        {
            if (string.IsNullOrWhiteSpace(culture.Name))
            {
                continue;
            }

            if (!IsCultureEligible(culture))
            {
                continue;
            }

            RegionInfo region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var code = region.ISOCurrencySymbol;
            if (string.IsNullOrWhiteSpace(code) || byCode.ContainsKey(code))
            {
                continue;
            }

            byCode[code] = BuildCurrencyInfo(region, culture.NumberFormat, culture.Name);
        }

        return byCode.Values.ToList();
    }
}
