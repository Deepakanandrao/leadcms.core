// <copyright file="LanguageHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Helpers;

public static class LanguageHelper
{
    public const string DefaultFallbackLanguage = "en";

    public static string[] GetSupportedLanguages(IConfiguration configuration)
    {
        var supportedLanguages = configuration.GetSection("SupportedLanguages").Get<string[]>() ?? Array.Empty<string>();
        if (supportedLanguages.Length == 0)
        {
            supportedLanguages = new[] { DefaultFallbackLanguage };
        }

        return supportedLanguages;
    }

    public static string GetDefaultLanguage(IConfiguration configuration)
    {
        return GetSupportedLanguages(configuration)[0];
    }
}
