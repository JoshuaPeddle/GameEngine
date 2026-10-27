using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEngine.Core.Utils;

public sealed class LevelValueJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonElement.ParseValue(ref reader);

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case JsonElement element: element.WriteTo(writer); break;
            case string text: writer.WriteStringValue(text); break;
            case bool boolean: writer.WriteBooleanValue(boolean); break;
            case byte number: writer.WriteNumberValue(number); break;
            case sbyte number: writer.WriteNumberValue(number); break;
            case short number: writer.WriteNumberValue(number); break;
            case ushort number: writer.WriteNumberValue(number); break;
            case int number: writer.WriteNumberValue(number); break;
            case uint number: writer.WriteNumberValue(number); break;
            case long number: writer.WriteNumberValue(number); break;
            case ulong number: writer.WriteNumberValue(number); break;
            case float number: writer.WriteNumberValue(number); break;
            case double number: writer.WriteNumberValue(number); break;
            case decimal number: writer.WriteNumberValue(number); break;
            case IDictionary<string, object> properties:
                writer.WriteStartObject();
                foreach (var property in properties)
                {
                    writer.WritePropertyName(property.Key);
                    WriteValue(writer, property.Value, options);
                }
                writer.WriteEndObject();
                break;
            case IEnumerable items:
                writer.WriteStartArray();
                foreach (var item in items) WriteValue(writer, item, options);
                writer.WriteEndArray();
                break;
            default: throw new NotSupportedException("Level values must be JSON elements, strings, booleans, numbers, string-keyed dictionaries or arrays.");
        }
    }

    private void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else Write(writer, value, options);
    }
}
