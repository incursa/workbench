using System.Text;
using System.Text.Json;
using System.Linq;
using System.Collections;

namespace Workbench;

public static class JsonWriter
{
    public static string Serialize(object? value, bool indented = true)
    {
        var options = new JsonWriterOptions { Indented = indented };
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            WriteValue(writer, value);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void WriteToConsole(object? value, bool indented = true)
    {
        Console.WriteLine(Serialize(value, indented));
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (value)
        {
            case JsonElement element:
                element.WriteTo(writer);
                return;
            case string str:
                writer.WriteStringValue(str);
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case int intValue:
                writer.WriteNumberValue(intValue);
                return;
            case long longValue:
                writer.WriteNumberValue(longValue);
                return;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                return;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                return;
            case DateTime dateTime:
                writer.WriteStringValue(dateTime.ToString("O"));
                return;
            case Dictionary<string, object?> dict:
                WriteObject(writer, dict);
                return;
            case Dictionary<object, object> legacyDict:
                WriteObject(writer, legacyDict.ToDictionary(
                    kvp => kvp.Key.ToString() ?? string.Empty,
                    kvp => (object?)kvp.Value,
                    StringComparer.OrdinalIgnoreCase));
                return;
            case IEnumerable enumerable when value is not string:
                WriteArray(writer, enumerable);
                return;
        }

        writer.WriteStringValue(value.ToString());
    }

    private static void WriteObject(Utf8JsonWriter writer, Dictionary<string, object?> dict)
    {
        writer.WriteStartObject();
        foreach (var (key, value) in dict)
        {
            writer.WritePropertyName(key);
            WriteValue(writer, value);
        }
        writer.WriteEndObject();
    }

    private static void WriteArray(Utf8JsonWriter writer, IEnumerable list)
    {
        writer.WriteStartArray();
        foreach (var entry in list)
        {
            WriteValue(writer, entry);
        }
        writer.WriteEndArray();
    }
}
