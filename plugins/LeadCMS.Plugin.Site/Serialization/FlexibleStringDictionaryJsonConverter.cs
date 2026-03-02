// <copyright file="FlexibleStringDictionaryJsonConverter.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeadCMS.Plugin.Site.Serialization;

public class FlexibleStringDictionaryJsonConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected JSON object for dictionary.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return result;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected property name token.");
            }

            var key = reader.GetString() ?? string.Empty;

            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of JSON while reading dictionary value.");
            }

            using var document = JsonDocument.ParseValue(ref reader);
            var element = document.RootElement;

            result[key] = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => element.GetRawText(),
            };
        }

        throw new JsonException("Unexpected end of JSON while reading dictionary.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var (key, itemValue) in value)
        {
            writer.WriteString(key, itemValue);
        }

        writer.WriteEndObject();
    }
}