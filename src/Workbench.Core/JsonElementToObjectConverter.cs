using System.Globalization;
using System.Text.Json;

namespace Workbench.Core;

internal static class JsonElementToObjectConverter
{
    public static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Expected a JSON object but found '{element.ValueKind}'.");
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ConvertValue(property.Value);
        }

        return result;
    }

    private static object? ConvertValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(value),
            JsonValueKind.Array => value.EnumerateArray().Select(ConvertValue).ToList(),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => TryReadNumber(value),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => value.GetRawText(),
        };
    }

    private static object TryReadNumber(JsonElement value)
    {
        if (value.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (value.TryGetDecimal(out var decimalValue))
        {
            return decimalValue;
        }

        return value.GetDouble().ToString(CultureInfo.InvariantCulture);
    }
}
