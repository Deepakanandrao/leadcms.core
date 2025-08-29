// <copyright file="TranslationConflictException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Exceptions;

/// <summary>
/// Exception thrown when a translation already exists for the specified language.
/// </summary>
public class TranslationConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationConflictException"/> class.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    /// <param name="entityId">The ID of the entity.</param>
    /// <param name="language">The language code.</param>
    public TranslationConflictException(string entityType, int entityId, string language)
        : base($"A translation for {entityType} (ID: {entityId}) already exists for language '{language}'.")
    {
        EntityType = entityType;
        EntityId = entityId;
        Language = language;
    }

    /// <summary>
    /// Gets the type of the entity.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Gets the ID of the entity.
    /// </summary>
    public int EntityId { get; }

    /// <summary>
    /// Gets the language code.
    /// </summary>
    public string Language { get; }
}
