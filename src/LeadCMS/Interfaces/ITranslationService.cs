// <copyright file="ITranslationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Entities;
using LeadCMS.Enums;

namespace LeadCMS.Interfaces;

/// <summary>
/// Service for handling translation operations on translatable entities.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Creates a translation draft for the specified entity.
    /// </summary>
    /// <typeparam name="T">The entity type that implements ITranslatable.</typeparam>
    /// <param name="entityId">The ID of the entity to translate.</param>
    /// <param name="language">The target language for the translation.</param>
    /// <param name="transformerType">The transformation strategy to use.</param>
    /// <returns>The translation draft.</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the entity is not found.</exception>
    /// <exception cref="TranslationConflictException">Thrown when a translation already exists for the specified language.</exception>
    /// <exception cref="NotTranslatableException">Thrown when the entity does not support translations.</exception>
    /// <exception cref="UnsupportedLanguageException">Thrown when the language is not supported.</exception>
    Task<T> CreateTranslationDraftAsync<T>(int entityId, string language, TranslationTransformerType transformerType)
        where T : BaseEntityWithId, ITranslatable;

    /// <summary>
    /// Gets all existing translations for the specified entity.
    /// </summary>
    /// <typeparam name="T">The entity type that implements ITranslatable.</typeparam>
    /// <param name="entityId">The ID of the entity to get translations for.</param>
    /// <returns>A list of all translations for the entity.</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the entity is not found.</exception>
    /// <exception cref="NotTranslatableException">Thrown when the entity does not support translations.</exception>
    Task<List<T>> GetTranslationsAsync<T>(int entityId)
        where T : BaseEntityWithId, ITranslatable;
}
