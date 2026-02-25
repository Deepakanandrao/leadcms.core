// <copyright file="BaseControllerWithImport.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using AutoMapper;
using LeadCMS.Data;
using LeadCMS.DataAnnotations;
using LeadCMS.DTOs;
using LeadCMS.Entities;
using LeadCMS.Infrastructure;
using LeadCMS.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadCMS.Controllers;

public class BaseControllerWithImport<T, TC, TU, TD, TI> : BaseController<T, TC, TU, TD>
    where T : BaseEntityWithId, new()
    where TC : class
    where TU : class
    where TD : class
    where TI : BaseImportDtoWithIdAndSource
{
    protected AdditionalImportChecker additionalImportChecker = new AdditionalImportChecker();

    public BaseControllerWithImport(PgDbContext dbContext, IMapper mapper, EsDbContext esDbContext, QueryProviderFactory<T> queryProviderFactory, ISyncService syncService)
        : base(dbContext, mapper, esDbContext, queryProviderFactory, syncService)
    {
    }

    [HttpPost]
    [Route("import")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public virtual async Task<ActionResult<ImportResult>> Import([FromBody] List<TI> importRecords)
    {
        var result = new ImportResult();

        var newRecords = new List<T>();
        var duplicates = new Dictionary<TI, object>();

        dbContext.IsImportRequest = true;

        var typeIdentifiersMap = BuildTypeIdentifiersMap(importRecords);

        var relatedObjectsMap = BuildRelatedObjectsMap(typeIdentifiersMap, importRecords, newRecords, duplicates);

        var relatedTObjectsMap = relatedObjectsMap[typeof(T)];

        additionalImportChecker.SetData(importRecords);

        for (var i = 0; i < importRecords.Count; i++)
        {
            var importRecord = importRecords[i];

            if (!additionalImportChecker.Check(i, result))
            {
                continue;
            }

            if (duplicates.TryGetValue(importRecord, out var identifierValue))
            {
                result.AddError(i, $"Row number {i} has a duplicate indentification value {identifierValue} and will be skipped. Please ensure that each record has a unique key to avoid data loss.");
                continue;
            }

            BaseEntityWithId? dbRecord = null;

            foreach (var identifierProperty in relatedTObjectsMap.IdentifierPropertyNames)
            {
                var identifierPropertyInfo = importRecord.GetType().GetProperty(identifierProperty)!;

                var propertyValue = identifierPropertyInfo.GetValue(importRecord);

                if (propertyValue != null && relatedTObjectsMap[identifierProperty].TryGetValue(propertyValue, out dbRecord))
                {
                    mapper.Map(importRecord, dbRecord);
                    FixDateKindIfNeeded((T)dbRecord!);
                    break;
                }
            }

            // Try composite key matching
            if (dbRecord == null && relatedTObjectsMap.CompositeKeyMap != null)
            {
                var compositeKey = BuildCompositeKeyForLookup(importRecord, relatedTObjectsMap, relatedObjectsMap);
                if (compositeKey != null && relatedTObjectsMap.CompositeKeyMap.TryGetValue(compositeKey, out var existingRecord))
                {
                    dbRecord = existingRecord;
                    mapper.Map(importRecord, dbRecord);
                    FixDateKindIfNeeded((T)dbRecord!);
                }
            }

            if (dbRecord == null)
            {
                dbRecord = mapper.Map<T>(importRecord);
                FixDateKindIfNeeded((T)dbRecord!);
                newRecords.Add((T)dbRecord!);
            }

            for (var j = 0; j < relatedTObjectsMap.SurrogateKeyPropertyNames.Count; j++)
            {
                var surrogateKeyAttribute = relatedTObjectsMap.SurrogateKeyPropertyAttributes[j];
                var surrogateKeyPropertyInfo = importRecord.GetType().GetProperty(relatedTObjectsMap.SurrogateKeyPropertyNames[j])!;

                var surrogateKeyValue = surrogateKeyPropertyInfo.GetValue(importRecord);

                BaseEntityWithId? relatedObject;

                if (surrogateKeyValue != null && surrogateKeyValue.ToString() != string.Empty)
                {
                    var relatedObjectMap = relatedObjectsMap[surrogateKeyAttribute.RelatedType];

                    if (relatedObjectMap[surrogateKeyAttribute.RelatedTypeUniqeIndex].TryGetValue(surrogateKeyValue, out relatedObject) && relatedObject != null)
                    {
                        var navigationPropertyName = surrogateKeyAttribute.SourceForeignKey.Replace("Id", string.Empty);

                        var targetNavigationPropertyInfo = dbRecord.GetType().GetProperty(navigationPropertyName);

                        if (targetNavigationPropertyInfo == null)
                        {
                            throw new ServerException($"Entity {dbRecord.GetType().Name} does not have a required navigation property '{navigationPropertyName}'");
                        }

                        targetNavigationPropertyInfo.SetValue(dbRecord, relatedObject);
                    }
                    else
                    {
                        result.AddError(i, $"Row number {i} references {surrogateKeyAttribute.RelatedType} that does not exist in the database ({surrogateKeyAttribute.RelatedTypeUniqeIndex} = {surrogateKeyValue}).");

                        if (newRecords.Contains((T)dbRecord))
                        {
                            newRecords.Remove((T)dbRecord);
                        }
                    }
                }
            }
        }

        await SaveRangeAsync(newRecords);

        var entriesByState = dbContext.ChangeTracker
            .Entries()
            .Where(e => e.Entity is T && (
                e.State == EntityState.Added
                || e.State == EntityState.Modified))
            .GroupBy(e => e.State)
            .ToDictionary(g => g.Key, g => g.ToList());

        result.Skipped = importRecords.Count - result.Failed;

        if (entriesByState.TryGetValue(EntityState.Added, out var added))
        {
            result.Added = added.Count;
            result.Skipped -= result.Added;
        }

        if (entriesByState.TryGetValue(EntityState.Modified, out var modified))
        {
            result.Updated = modified.Count;
            result.Skipped -= result.Updated;
        }

        await dbContext.SaveChangesAsync();

        // Call the hook after successful import
        await OnAfterImportAsync(newRecords, importRecords);

        return Ok(result);
    }

    /// <summary>
    /// Called after entities are successfully imported. Override this method to add custom post-import logic.
    /// </summary>
    /// <param name="importedEntities">The list of imported entities.</param>
    /// <param name="importRecords">The original import records.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected virtual async Task OnAfterImportAsync(List<T> importedEntities, List<TI> importRecords)
    {
        // Default implementation does nothing
        await Task.CompletedTask;
    }

    protected virtual async Task SaveRangeAsync(List<T> newRecords)
    {
        await dbSet.AddRangeAsync(newRecords);
    }

    private void FixDateKindIfNeeded(T record)
    {
        if (record is IHasCreatedAt createdAtRecord && createdAtRecord.CreatedAt.Kind != DateTimeKind.Utc)
        {
            createdAtRecord.CreatedAt = createdAtRecord.CreatedAt.ToUniversalTime();
        }

        if (record is IHasUpdatedAt updatedAtRecord && updatedAtRecord.UpdatedAt is not null && updatedAtRecord.UpdatedAt.Value.Kind != DateTimeKind.Utc)
        {
            updatedAtRecord.UpdatedAt = updatedAtRecord.UpdatedAt.Value.ToUniversalTime();
        }
    }

    private TypedRelatedObjectsMap BuildRelatedObjectsMap(TypeIdentifiers typeIdentifiersMap, List<TI> importRecords, List<T> newRecords, Dictionary<TI, object> duplicates)
    {
        var typedRelatedObjectsMap = new TypedRelatedObjectsMap();

        foreach (var type in typeIdentifiersMap.Keys)
        {
            var identifierValues = typeIdentifiersMap[type];

            var relatedObjectsMap = new RelatedObjectsMap
            {
                IdentifierPropertyNames = identifierValues.IdentifierPropertyNames,
                SurrogateKeyPropertyNames = identifierValues.SurrogateKeyPropertyNames,
                SurrogateKeyPropertyAttributes = identifierValues.SurrogateKeyPropertyAttributes,
            };

            var mappedObjectsCash = new Dictionary<TI, object>();

            foreach (var propertyName in identifierValues.Keys)
            {
                var existingRecordsProperty = type.GetProperty(propertyName)!;
                var importRecordsProperty = typeof(TI).GetProperty(propertyName)!;

                var propertyValues = identifierValues[propertyName];

                var predicate = BuildPropertyValuesPredicate(type, propertyName, propertyValues);

                var existingObjectsDict = dbContext.SetDbEntity(type)
                                        .Where(predicate).AsQueryable()
                                        .ToDictionary(x => existingRecordsProperty.GetValue(x)!, x => x);

                Dictionary<object, TI>? importRecordsDict = null;

                if (type == typeof(T))
                {
                    var uniqueGroups = importRecords
                                        .Select(x => new { Identifier = importRecordsProperty.GetValue(x), Record = x })
                                        .Where(x => x.Identifier != null && x.Identifier.ToString() != "0" && x.Identifier.ToString() != string.Empty)
                                        .GroupBy(x => x.Identifier!);

                    importRecordsDict = uniqueGroups.ToDictionary(g => g.Key, g => g.First().Record);

                    duplicates.AddRangeIfNotExists(uniqueGroups
                                        .Where(g => g.Count() > 1)
                                        .SelectMany(g => g.Skip(1))
                                        .ToDictionary(x => x.Record, x => x.Identifier!));
                }

                relatedObjectsMap[propertyName] = propertyValues
                       .Select(uid =>
                        {
                            existingObjectsDict.TryGetValue(uid, out var record);

                            if (type == typeof(T) && importRecordsDict!.TryGetValue(uid, out var importRecord))
                            {
                                if (record == null && !mappedObjectsCash.TryGetValue(importRecord, out record))
                                {
                                    record = mapper.Map<T>(importRecord);
                                    FixDateKindIfNeeded((T)record);
                                    newRecords.Add((T)record);
                                }

                                mappedObjectsCash[importRecord] = record;
                            }

                            return new { Uid = uid, Record = record };
                        })
                       .ToDictionary(x => x.Uid, x => x.Record as BaseEntityWithId);
            }

            typedRelatedObjectsMap[type] = relatedObjectsMap;
        }

        // Handle composite alternate key (processed after all types to ensure surrogate FK maps are available)
        if (typeIdentifiersMap[typeof(T)].CompositeKeyPropertyNames != null)
        {
            var tRelatedMap = typedRelatedObjectsMap[typeof(T)];
            tRelatedMap.CompositeKeyPropertyNames = typeIdentifiersMap[typeof(T)].CompositeKeyPropertyNames;
            BuildCompositeKeyLookup(tRelatedMap, typedRelatedObjectsMap, importRecords, duplicates);
        }

        return typedRelatedObjectsMap;
    }

    private TypeIdentifiers BuildTypeIdentifiersMap(List<TI> importRecords)
    {
        var typeIdentifiersMap = new TypeIdentifiers
        {
            { typeof(T), new IdentifierValues() },
        };

        var idValues = importRecords
                    .Where(r => r.Id is not null && r.Id > 0)
                    .Select(r => (object)r.Id!.Value)
                    .Distinct()
                    .ToList();

        if (idValues.Count > 0)
        {
            typeIdentifiersMap[typeof(T)]["Id"] = idValues;
            typeIdentifiersMap[typeof(T)].IdentifierPropertyNames.Add("Id");
        }

        var alternateKeyPropertyNames = FindAlternateKeyPropertyNames();

        if (alternateKeyPropertyNames != null)
        {
            if (alternateKeyPropertyNames.Length == 1)
            {
                // Single-property alternate key
                var property = typeof(TI).GetProperty(alternateKeyPropertyNames[0])!;

                var uniqueValues = importRecords
                                       .Where(r => property.GetValue(r) != null && property.GetValue(r)!.ToString() != string.Empty)
                                       .Select(r => property.GetValue(r))
                                       .Distinct()
                                       .ToList();

                if (uniqueValues.Count > 0)
                {
                    typeIdentifiersMap[typeof(T)][alternateKeyPropertyNames[0]] = uniqueValues!;
                    typeIdentifiersMap[typeof(T)].IdentifierPropertyNames.Add(alternateKeyPropertyNames[0]);
                }
            }
            else
            {
                // Composite alternate key — store property names for resolution in BuildRelatedObjectsMap
                typeIdentifiersMap[typeof(T)].CompositeKeyPropertyNames = alternateKeyPropertyNames;
            }
        }

        var importProperties = typeof(TI).GetProperties();

        foreach (var property in importProperties)
        {
            if (property.GetCustomAttributes(typeof(SurrogateForeignKeyAttribute), true).FirstOrDefault() is not SurrogateForeignKeyAttribute surrogateForeignKeyAttribute)
            {
                continue;
            }

            var type = surrogateForeignKeyAttribute.RelatedType;

            var identifierName = surrogateForeignKeyAttribute.RelatedTypeUniqeIndex;

            var identifierValues = importRecords
                                   .Where(r => property.GetValue(r) != null && property.GetValue(r)!.ToString() != string.Empty)
                                   .Select(r => property.GetValue(r))
                                   .Distinct()
                                   .ToList();

            if (identifierValues.Count == 0)
            {
                continue;
            }

            if (!typeIdentifiersMap.ContainsKey(type))
            {
                typeIdentifiersMap[type] = new IdentifierValues();
            }

            if (!typeIdentifiersMap[type].ContainsKey(identifierName))
            {
                typeIdentifiersMap[type][identifierName] = new List<object>();
            }

            typeIdentifiersMap[type][identifierName].AddRange(identifierValues!);

            typeIdentifiersMap[type][identifierName] = typeIdentifiersMap[type][identifierName].Distinct().ToList();

            typeIdentifiersMap[typeof(T)].SurrogateKeyPropertyNames.Add(property.Name);
            typeIdentifiersMap[typeof(T)].SurrogateKeyPropertyAttributes.Add(surrogateForeignKeyAttribute);
        }

        return typeIdentifiersMap;
    }

    private string[]? FindAlternateKeyPropertyNames()
    {
        var indexAttribute = typeof(T).GetCustomAttributes(typeof(IndexAttribute), true)
                               .Select(a => (IndexAttribute)a)
                               .FirstOrDefault(a => a.IsUnique);

        if (indexAttribute != null)
        {
            return indexAttribute.PropertyNames.ToArray();
        }

        var surrogateIdentityAttribute = typeof(T).GetCustomAttributes(typeof(SurrogateIdentityAttribute), true)
                                   .Select(a => (SurrogateIdentityAttribute)a)
                                   .FirstOrDefault();

        if (surrogateIdentityAttribute != null)
        {
            return new[] { surrogateIdentityAttribute.PropertyName };
        }

        return null;
    }

    private Func<object, bool> BuildPropertyValuesPredicate(Type targetType, string propertyName, List<object> propertyValues)
    {
        // Get the property info for the property name
        var propertyInfo = targetType.GetProperty(propertyName);

        // Create a parameter expression for the object type
        var objectParam = Expression.Parameter(typeof(object), "o");

        // Convert the object parameter to the target type
        var convertedParam = Expression.Convert(objectParam, targetType);

        // Create the property access expression for the property name
        var propertyAccess = Expression.Property(convertedParam, propertyInfo!);

        // Convert the property access expression to type object
        var convertedPropertyAccess = Expression.Convert(propertyAccess, typeof(object));

        // Create the constant expression for the property values
        var valuesConstant = Expression.Constant(propertyValues, typeof(List<object>));
        var containsMethod = typeof(List<object>).GetMethod("Contains", new[] { typeof(object) });
        var containsExpression = Expression.Call(valuesConstant, containsMethod!, convertedPropertyAccess);

        // Create the lambda expression for the predicate
        var lambdaExpression = Expression.Lambda<Func<object, bool>>(containsExpression, objectParam);

        return lambdaExpression.Compile();
    }

    private void BuildCompositeKeyLookup(
        RelatedObjectsMap tRelatedMap,
        TypedRelatedObjectsMap allRelatedMaps,
        List<TI> importRecords,
        Dictionary<TI, object> duplicates)
    {
        var compositePropertyNames = tRelatedMap.CompositeKeyPropertyNames!;
        var entityProperties = compositePropertyNames.Select(n => typeof(T).GetProperty(n)!).ToArray();
        var importProperties = compositePropertyNames.Select(n => typeof(TI).GetProperty(n)).ToArray();

        // Resolve composite key values from import records, using surrogate FK resolution when needed
        var resolvedKeys = new List<(CompositeKey Key, TI Record)>();
        foreach (var importRecord in importRecords)
        {
            var keyValues = new object?[compositePropertyNames.Length];
            var allResolved = true;

            for (var i = 0; i < compositePropertyNames.Length; i++)
            {
                var value = importProperties[i]?.GetValue(importRecord);
                var isRequired = entityProperties[i].GetCustomAttribute(typeof(RequiredAttribute)) != null;

                if (value == null || value.ToString() == "0" || value.ToString() == string.Empty)
                {
                    value = ResolveFKValueFromSurrogateKey(importRecord, compositePropertyNames[i], tRelatedMap, allRelatedMaps);
                }

                if (value == null || value.ToString() == "0" || value.ToString() == string.Empty)
                {
                    if (isRequired)
                    {
                        allResolved = false;
                        break;
                    }

                    // For non-required (nullable) properties, null is a valid composite key value
                    keyValues[i] = null;
                    continue;
                }

                keyValues[i] = value;
            }

            if (allResolved)
            {
                resolvedKeys.Add((new CompositeKey(keyValues!), importRecord));
            }
        }

        // Detect duplicates in import batch
        var groups = resolvedKeys.GroupBy(x => x.Key);
        var duplicateRecords = groups
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Skip(1).Select(dup => new { dup.Record, Key = (object)g.Key }));

        foreach (var dup in duplicateRecords)
        {
            if (!duplicates.ContainsKey(dup.Record))
            {
                duplicates[dup.Record] = dup.Key;
            }
        }

        // Query DB for existing records using first key component for efficiency
        var firstComponentValues = resolvedKeys
            .Select(x => x.Key.Values[0])
            .Distinct()
            .ToList();

        if (firstComponentValues.Count == 0)
        {
            tRelatedMap.CompositeKeyMap = new Dictionary<CompositeKey, BaseEntityWithId>();
            return;
        }

        var predicate = BuildPropertyValuesPredicate(typeof(T), compositePropertyNames[0], firstComponentValues);
        var candidateRecords = dbContext.SetDbEntity(typeof(T)).Where(predicate).AsQueryable().ToList();

        tRelatedMap.CompositeKeyMap = new Dictionary<CompositeKey, BaseEntityWithId>();
        foreach (var record in candidateRecords)
        {
            var entityKeyValues = entityProperties.Select(p => p.GetValue(record)!).ToArray();
            var key = new CompositeKey(entityKeyValues);
            tRelatedMap.CompositeKeyMap[key] = (BaseEntityWithId)record;
        }
    }

    private CompositeKey? BuildCompositeKeyForLookup(TI importRecord, RelatedObjectsMap relatedTObjectsMap, TypedRelatedObjectsMap allRelatedMaps)
    {
        var compositePropertyNames = relatedTObjectsMap.CompositeKeyPropertyNames!;
        var entityProperties = compositePropertyNames.Select(n => typeof(T).GetProperty(n)!).ToArray();
        var keyValues = new object?[compositePropertyNames.Length];

        for (var i = 0; i < compositePropertyNames.Length; i++)
        {
            var prop = typeof(TI).GetProperty(compositePropertyNames[i]);
            var value = prop?.GetValue(importRecord);
            var isRequired = entityProperties[i].GetCustomAttribute(typeof(RequiredAttribute)) != null;

            if (value == null || value.ToString() == "0" || value.ToString() == string.Empty)
            {
                value = ResolveFKValueFromSurrogateKey(importRecord, compositePropertyNames[i], relatedTObjectsMap, allRelatedMaps);
            }

            if (value == null || value.ToString() == "0" || value.ToString() == string.Empty)
            {
                if (isRequired)
                {
                    return null;
                }

                // For non-required (nullable) properties, null is a valid composite key value
                keyValues[i] = null;
                continue;
            }

            keyValues[i] = value;
        }

        return new CompositeKey(keyValues!);
    }

    private object? ResolveFKValueFromSurrogateKey(TI importRecord, string fkPropertyName, RelatedObjectsMap relatedMap, TypedRelatedObjectsMap allRelatedMaps)
    {
        for (var j = 0; j < relatedMap.SurrogateKeyPropertyNames.Count; j++)
        {
            var surrogateAttr = relatedMap.SurrogateKeyPropertyAttributes[j];
            if (surrogateAttr.SourceForeignKey != fkPropertyName)
            {
                continue;
            }

            var surrogateKeyProp = typeof(TI).GetProperty(relatedMap.SurrogateKeyPropertyNames[j]);
            var surrogateKeyValue = surrogateKeyProp?.GetValue(importRecord);

            if (surrogateKeyValue == null || surrogateKeyValue.ToString() == string.Empty)
            {
                continue;
            }

            if (allRelatedMaps.TryGetValue(surrogateAttr.RelatedType, out var relatedTypeMap)
                && relatedTypeMap.TryGetValue(surrogateAttr.RelatedTypeUniqeIndex, out var relatedEntityMap)
                && relatedEntityMap.TryGetValue(surrogateKeyValue, out var relatedEntity)
                && relatedEntity != null)
            {
                return relatedEntity.Id;
            }
        }

        return null;
    }

    protected class AdditionalImportChecker
    {
        public virtual void SetData(List<TI> importRecords)
        {
        }

        public virtual bool Check(int index, ImportResult result)
        {
            return true;
        }
    }
}

public class ImportResult
{
    public int Added { get; set; }

    public int Updated { get; set; }

    public int Failed { get; set; }

    public int Skipped { get; set; }

    public List<ImportError>? Errors { get; set; }

    public void AddError(int row, string message)
    {
        Failed++;
        Errors ??= new List<ImportError>();

        Errors.Add(new ImportError
        {
            Row = row,
            Message = message,
        });
    }
}

public class ImportError
{
    public int Row { get; set; }

    public string Message { get; set; } = string.Empty;
}

internal class TypedRelatedObjectsMap : Dictionary<Type, RelatedObjectsMap>
{
}

internal class RelatedObjectsMap : Dictionary<string, Dictionary<object, BaseEntityWithId?>>
{
    public List<string> IdentifierPropertyNames { get; set; } = new List<string>();

    public List<string> SurrogateKeyPropertyNames { get; set; } = new List<string>();

    public List<SurrogateForeignKeyAttribute> SurrogateKeyPropertyAttributes { get; set; } = new List<SurrogateForeignKeyAttribute>();

    public string[]? CompositeKeyPropertyNames { get; set; }

    public Dictionary<CompositeKey, BaseEntityWithId>? CompositeKeyMap { get; set; }
}

internal class TypeIdentifiers : Dictionary<Type, IdentifierValues>
{
}

internal class IdentifierValues : Dictionary<string, List<object>>
{
    public List<string> IdentifierPropertyNames { get; set; } = new List<string>();

    public List<string> SurrogateKeyPropertyNames { get; set; } = new List<string>();

    public List<SurrogateForeignKeyAttribute> SurrogateKeyPropertyAttributes { get; set; } = new List<SurrogateForeignKeyAttribute>();

    public string[]? CompositeKeyPropertyNames { get; set; }
}

internal class CompositeKey : IEquatable<CompositeKey>
{
    private readonly object[] values;

    public CompositeKey(params object[] values)
    {
        this.values = values;
    }

    public object[] Values => values;

    public override bool Equals(object? obj) => obj is CompositeKey other && Equals(other);

    public bool Equals(CompositeKey? other)
    {
        if (other is null || values.Length != other.values.Length)
        {
            return false;
        }

        for (var i = 0; i < values.Length; i++)
        {
            if (!Equals(values[i], other.values[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var v in values)
        {
            hash.Add(v);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(", ", values);
}