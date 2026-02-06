// <copyright file="DtoCleanupHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace LeadCMS.Helpers;

/// <summary>
/// Static helper class for cleaning up DTO objects by removing second-level navigation properties
/// to prevent circular references and reduce response size.
/// </summary>
public static class DtoCleanupHelper
{
    private static readonly ConcurrentDictionary<Type, Dictionary<PropertyInfo, List<PropertyInfo>>> SecondLevelDtosCache
        = new ConcurrentDictionary<Type, Dictionary<PropertyInfo, List<PropertyInfo>>>();

    /// <summary>
    /// Removes second-level navigation properties from a collection of DTOs.
    /// </summary>
    /// <typeparam name="TDto">The DTO type.</typeparam>
    /// <param name="items">The collection of DTO items to clean up.</param>
    public static void RemoveSecondLevelObjects<TDto>(IList<TDto> items)
        where TDto : class
    {
        if (items == null || !items.Any())
        {
            return;
        }

        var secondLevelRefs = GetSecondLevelDTOs<TDto>();

        foreach (var item in items)
        {
            foreach (var kvp in secondLevelRefs)
            {
                var propertyObject = kvp.Key.GetValue(item);
                if (propertyObject != null)
                {
                    if (kvp.Key.PropertyType.GetInterface("IEnumerable") != null && kvp.Key.PropertyType.IsGenericType)
                    {
                        var enumerable = propertyObject as IEnumerable;
                        if (enumerable != null)
                        {
                            foreach (var obj in enumerable)
                            {
                                foreach (var childProperty in kvp.Value)
                                {
                                    childProperty.SetValue(obj, null);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var childProperty in kvp.Value)
                        {
                            childProperty.SetValue(propertyObject, null);
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<PropertyInfo, List<PropertyInfo>> GetSecondLevelDTOs<TDto>()
    {
        var dtoType = typeof(TDto);

        return SecondLevelDtosCache.GetOrAdd(dtoType, BuildSecondLevelDtos);
    }

    private static Dictionary<PropertyInfo, List<PropertyInfo>> BuildSecondLevelDtos(Type dtoType)
    {
        var result = new Dictionary<PropertyInfo, List<PropertyInfo>>();
        var properties = dtoType.GetProperties();

        foreach (var property in properties)
        {
            var propertyType = GetUnderlyingType(property);
            var nestedProperties = propertyType.GetProperties();

            foreach (var nestedProperty in nestedProperties.Where(IsNullableDto))
            {
                if (result.TryGetValue(property, out var existingList))
                {
                    existingList.Add(nestedProperty);
                }
                else
                {
                    result[property] = new List<PropertyInfo> { nestedProperty };
                }
            }
        }

        return result;
    }

    private static Type GetUnderlyingType(PropertyInfo property)
    {
        if (property.PropertyType.GetInterface("IEnumerable") != null && property.PropertyType.IsGenericType)
        {
            return property.PropertyType.GetGenericArguments()[0];
        }

        return property.PropertyType;
    }

    private static bool IsNullableDto(PropertyInfo property)
    {
        return IsNullableProperty(property) && IsDto(GetUnderlyingType(property));
    }

    private static bool IsNullableProperty(PropertyInfo property)
    {
        var context = new NullabilityInfoContext();
        var info = context.Create(property);
        return info.WriteState == NullabilityState.Nullable;
    }

    private static bool IsDto(Type type)
    {
        return type.IsClass && type != typeof(string) && type.Namespace?.StartsWith("LeadCMS.DTOs") == true;
    }
}