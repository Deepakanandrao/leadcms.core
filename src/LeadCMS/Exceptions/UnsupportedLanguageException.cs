// <copyright file="UnsupportedLanguageException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Exceptions;

/// <summary>
/// Exception thrown when a language is not supported for translations.
/// </summary>
public class UnsupportedLanguageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedLanguageException"/> class.
    /// </summary>
    /// <param name="language">The unsupported language code.</param>
    /// <param name="supportedLanguages">The list of supported language codes.</param>
    public UnsupportedLanguageException(string language, IEnumerable<string> supportedLanguages)
        : base($"Language '{language}' is not supported. Supported languages are: {string.Join(", ", supportedLanguages)}.")
    {
        Language = language;
        SupportedLanguages = supportedLanguages.ToList();
    }

    /// <summary>
    /// Gets the unsupported language code.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets the list of supported language codes.
    /// </summary>
    public List<string> SupportedLanguages { get; }
}
