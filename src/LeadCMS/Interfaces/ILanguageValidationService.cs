// <copyright file="ILanguageValidationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for validating supported languages.
/// </summary>
public interface ILanguageValidationService
{
    /// <summary>
    /// Validates that the specified language is supported.
    /// </summary>
    /// <param name="language">The language code to validate.</param>
    /// <exception cref="UnsupportedLanguageException">Thrown when the language is not supported.</exception>
    void ValidateLanguage(string language);

    /// <summary>
    /// Gets the list of supported languages.
    /// </summary>
    /// <returns>The list of supported language codes.</returns>
    List<string> GetSupportedLanguages();

    /// <summary>
    /// Checks if the specified language is supported.
    /// </summary>
    /// <param name="language">The language code to check.</param>
    /// <returns>True if the language is supported, false otherwise.</returns>
    bool IsLanguageSupported(string language);
}
