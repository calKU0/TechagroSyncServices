using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class FlexibleListConverter<T> : JsonConverter<List<T>>
{
    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return JsonSerializer.Deserialize<List<T>>(ref reader, options);
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            var singleItem = JsonSerializer.Deserialize<T>(ref reader, options);
            return new List<T> { singleItem };
        }
        else if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.String)
        {
            reader.Skip();
            return new List<T>();
        }

        throw new JsonException($"Unexpected token {reader.TokenType} when parsing List<{typeof(T).Name}>");
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}
