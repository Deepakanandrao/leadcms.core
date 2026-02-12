// <copyright file="LanguageValidationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Helpers;
using LeadCMS.Interfaces;

namespace LeadCMS.Services;

/// <summary>
/// Service for validating supported languages.
/// </summary>
public class LanguageValidationService : ILanguageValidationService
{
    private readonly List<string> supportedLanguages;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageValidationService"/> class.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    public LanguageValidationService(IConfiguration configuration)
    {
        supportedLanguages = LanguageHelper.GetSupportedLanguages(configuration).ToList();
    }

    /// <inheritdoc/>
    public void ValidateLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language cannot be null or empty.", nameof(language));
        }

        if (!IsLanguageSupported(language))
        {
            throw new UnsupportedLanguageException(language, supportedLanguages);
        }
    }

    /// <inheritdoc/>
    public List<string> GetSupportedLanguages()
    {
        return new List<string>(supportedLanguages);
    }

    /// <inheritdoc/>
    public bool IsLanguageSupported(string language)
    {
        return supportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);
    }
}
