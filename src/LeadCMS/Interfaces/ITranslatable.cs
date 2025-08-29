// <copyright file="ITranslatable.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

/// <summary>
/// Interface for entities that support translations.
/// </summary>
public interface ITranslatable
{
    /// <summary>
    /// Gets or sets the language of the entity.
    /// </summary>
    string Language { get; set; }

    /// <summary>
    /// Gets or sets the translation key that groups related translations together.
    /// </summary>
    string? TranslationKey { get; set; }
}
