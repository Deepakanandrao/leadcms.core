// <copyright file="TranslationService.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Drawing;
using System.Reflection;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.Entities;
using LeadCMS.Enums;
using LeadCMS.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Services;

/// <summary>
/// Service for handling translation operations on translatable entities.
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly PgDbContext dbContext;
    private readonly IMapper mapper;
    private readonly ILanguageValidationService languageValidationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="languageValidationService">The language validation service.</param>
    public TranslationService(PgDbContext dbContext, IMapper mapper, ILanguageValidationService languageValidationService)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
        this.languageValidationService = languageValidationService;
    }

    /// <inheritdoc/>
    public async Task<T> CreateTranslationDraftAsync<T>(int entityId, string language, TranslationTransformerType transformerType)
        where T : BaseEntityWithId, ITranslatable
    {
        // Validate the language is supported
        languageValidationService.ValidateLanguage(language);

        var entityType = typeof(T);

        // Get the DbSet for the entity type
        var dbSet = dbContext.Set<T>();

        // Find the original entity
        var originalEntity = await dbSet.FindAsync(entityId);
        if (originalEntity == null)
        {
            throw new EntityNotFoundException(entityType.Name, entityId.ToString());
        }

        // Verify the entity supports translations
        if (originalEntity is not ITranslatable)
        {
            throw new NotTranslatableException(entityType.Name);
        }

        // Ensure the original entity has a translation key
        if (string.IsNullOrEmpty(originalEntity.TranslationKey))
        {
            originalEntity.TranslationKey = Guid.NewGuid().ToString();
            await dbContext.SaveChangesAsync();
        }

        // Check if a translation already exists for this language and translation key
        var existingTranslation = await dbSet
            .Where(e => e.TranslationKey == originalEntity.TranslationKey &&
                       e.Language == language)
            .FirstOrDefaultAsync();

        if (existingTranslation != null)
        {
            throw new TranslationConflictException(entityType.Name, entityId, language);
        }

        // Create translation draft based on transformer type
        T translationDraft;

        switch (transformerType)
        {
            case TranslationTransformerType.EmptyCopy:
                translationDraft = CreateEmptyCopy<T>(originalEntity, language);
                break;

            case TranslationTransformerType.KeepOriginal:
                translationDraft = CreateKeepOriginalCopy<T>(originalEntity, language);
                break;

            default:
                throw new ArgumentException($"Unknown transformer type: {transformerType}", nameof(transformerType));
        }

        return translationDraft;
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetTranslationsAsync<T>(int entityId)
        where T : BaseEntityWithId, ITranslatable
    {
        var entityType = typeof(T);

        // Get the DbSet for the entity type
        var dbSet = dbContext.Set<T>();

        // Find the original entity
        var originalEntity = await dbSet.FindAsync(entityId);
        if (originalEntity == null)
        {
            throw new EntityNotFoundException(entityType.Name, entityId.ToString());
        }

        // Verify the entity supports translations
        if (originalEntity is not ITranslatable)
        {
            throw new NotTranslatableException(entityType.Name);
        }

        // If the original entity doesn't have a translation key, return only the original
        if (string.IsNullOrEmpty(originalEntity.TranslationKey))
        {
            return new List<T> { originalEntity };
        }

        // Get all entities with the same translation key (including the original)
        var translations = await dbSet
            .Where(e => e.TranslationKey == originalEntity.TranslationKey)
            .OrderBy(e => e.Id) // Order by ID to ensure consistent ordering
            .ToListAsync();

        return translations;
    }

    private static void ResetTrackingFields<T>(T entity)
        where T : BaseEntityWithId
    {
        var entityType = typeof(T);

        // Reset common tracking fields using reflection
        var trackingFields = new[]
        {
            "CreatedById", "CreatedByIp", "CreatedByUserAgent",
            "UpdatedById", "UpdatedByIp", "UpdatedByUserAgent",
        };

        foreach (var fieldName in trackingFields)
        {
            var property = entityType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite && property.PropertyType == typeof(string))
            {
                property.SetValue(entity, null);
            }
        }
    }

    private static T CreateEmptyCopy<T>(T originalEntity, string language)
        where T : BaseEntityWithId, ITranslatable
    {
        var newEntity = Activator.CreateInstance<T>();

        // Set the translation properties
        newEntity.Language = language;
        newEntity.TranslationKey = originalEntity.TranslationKey; // Use existing key

        return newEntity;
    }

    private T CreateKeepOriginalCopy<T>(T originalEntity, string language)
        where T : BaseEntityWithId, ITranslatable
    {
        // Use AutoMapper to create a copy
        var copy = mapper.Map<T>(originalEntity);

        // Reset the ID (since this will be a new entity)
        copy.Id = 0;

        // Set the new language
        copy.Language = language;

        // The translation key is already copied from the original entity

        // Reset audit fields that should not be copied
        if (copy is IHasCreatedAt createdAtEntity)
        {
            createdAtEntity.CreatedAt = default;
        }

        if (copy is IHasUpdatedAt updatedAtEntity)
        {
            updatedAtEntity.UpdatedAt = null;
        }

        // Reset tracking fields
        ResetTrackingFields(copy);

        copy.Source = "Translated from " + originalEntity.Id;

        return copy;
    }
}
