// <copyright file="NotTranslatableException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Exceptions;

/// <summary>
/// Exception thrown when an entity does not support translations.
/// </summary>
public class NotTranslatableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotTranslatableException"/> class.
    /// </summary>
    /// <param name="entityType">The type of the entity.</param>
    public NotTranslatableException(string entityType)
        : base($"Entity type '{entityType}' does not support translations.")
    {
        EntityType = entityType;
    }

    /// <summary>
    /// Gets the type of the entity.
    /// </summary>
    public string EntityType { get; }
}
