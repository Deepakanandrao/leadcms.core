// <copyright file="Optional.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper.Configuration.Annotations;

namespace LeadCMS.Infrastructure;

/// <summary>
/// Interface for DTOs that support tracking explicitly-null properties in PATCH requests.
/// This allows distinguishing between "property not provided" and "property set to null".
/// Implement this interface in any DTO that needs to track null values during PATCH operations.
/// </summary>
public interface IPatchDto
{
    /// <summary>
    /// Gets the collection of property names that were explicitly set to null in the JSON request.
    /// This is populated automatically during deserialization.
    /// </summary>
    [Ignore]
    [JsonIgnore]
    HashSet<string> NullProperties { get; }
}

/// <summary>
/// JSON converter factory that creates converters for DTOs implementing IPatchDto.
/// Tracks properties explicitly set to null during deserialization.
/// </summary>
public class PatchDtoConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(IPatchDto).IsAssignableFrom(typeToConvert);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter?)Activator.CreateInstance(
            typeof(PatchDtoConverter<>).MakeGenericType(typeToConvert));
    }
}

/// <summary>
/// JSON converter for IPatchDto-implementing types that tracks explicitly-null properties.
/// </summary>
public class PatchDtoConverter<T> : JsonConverter<T>
    where T : IPatchDto
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var json = doc.RootElement.GetRawText();

        // Deserialize using the inner options to avoid recursion
        var innerOptions = new JsonSerializerOptions(options);
        innerOptions.Converters.Clear();

        var convertersToAdd = options.Converters.Where(c => c is not PatchDtoConverterFactory);
        foreach (var converter in convertersToAdd)
        {
            innerOptions.Converters.Add(converter);
        }

        var obj = JsonSerializer.Deserialize<T>(json, innerOptions);

        if (obj != null)
        {
            // Track properties explicitly set to null
            var nullProperties = doc.RootElement
                .EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Null)
                .Select(p => p.Name);

            foreach (var propertyName in nullProperties)
            {
                obj.NullProperties.Add(propertyName);
            }
        }

        return obj;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Serialize using inner options to avoid recursion
        var innerOptions = new JsonSerializerOptions(options);
        innerOptions.Converters.Clear();

        var convertersToAdd = options.Converters.Where(c => c is not PatchDtoConverterFactory);
        foreach (var converter in convertersToAdd)
        {
            innerOptions.Converters.Add(converter);
        }

        JsonSerializer.Serialize(writer, value, innerOptions);
    }
}

/// <summary>
/// Helper methods for working with IPatchDto implementations.
/// </summary>
public static class PatchDtoExtensions
{
    /// <summary>
    /// Applies explicitly-null properties from a DTO to an entity after AutoMapper mapping.
    /// This handles properties that were explicitly set to null in the JSON request.
    /// </summary>
    /// <typeparam name="TDto">The DTO type implementing IPatchDto.</typeparam>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="dto">The DTO containing the NullProperties collection.</param>
    /// <param name="entity">The entity to apply null values to.</param>
    public static void ApplyNullProperties<TDto, TEntity>(this TDto dto, TEntity entity)
        where TDto : IPatchDto
        where TEntity : class
    {
        foreach (var propertyName in dto.NullProperties)
        {
            var propertyInfo = typeof(TEntity).GetProperty(
                propertyName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

            if (propertyInfo != null && propertyInfo.CanWrite)
            {
                // Skip required properties (they can't be set to null)
                var isRequired = propertyInfo.GetCustomAttributes(
                    typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), true).Any();

                if (!isRequired)
                {
                    propertyInfo.SetValue(entity, null);
                }
            }
        }
    }
}
