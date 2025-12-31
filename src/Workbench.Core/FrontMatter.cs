using System.Collections;
using System.Linq;

namespace Workbench;

public sealed class FrontMatter
{
    public IDictionary<string, object?> Data { get; }
    public string Body { get; }

    public FrontMatter(IDictionary<string, object?> data, string body)
    {
        Data = data;
        Body = body;
    }

    public static bool TryParse(string content, out FrontMatter? frontMatter, out string? error)
    {
        frontMatter = null;
        error = null;
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 3 || !string.Equals(lines[0].Trim(), "---", StringComparison.OrdinalIgnoreCase))
        {
            error = "Missing front matter start delimiter.";
            return false;
        }

        var endIndex = Array.FindIndex(lines, 1, line => string.Equals(line.Trim(), "---", StringComparison.OrdinalIgnoreCase));
        if (endIndex <= 1)
        {
            error = "Missing front matter end delimiter.";
            return false;
        }

        var yamlText = string.Join("\n", lines[1..endIndex]);
        var body = string.Join("\n", lines[(endIndex + 1)..]).TrimStart('\n');

        try
        {
            if (!TryParseYaml(yamlText, out var data, out error))
            {
                return false;
            }
            frontMatter = new FrontMatter(data, body);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    public string Serialize()
    {
        var yaml = SerializeMap(Data, indent: 0).TrimEnd('\n');
        return $"---\n{yaml}\n---\n\n{Body}".TrimEnd() + "\n";
    }

    private static bool TryParseYaml(string yamlText, out Dictionary<string, object?> data, out string? error)
    {
        data = new Dictionary<string, object?>(StringComparer.Ordinal);
        error = null;
        var parseError = (string?)null;
        var lines = yamlText.Replace("\r\n", "\n").Split('\n');
        var index = 0;

        bool ParseMap(int indent, out Dictionary<string, object?> map)
        {
            map = new Dictionary<string, object?>(StringComparer.Ordinal);
            while (index < lines.Length)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                {
                    index++;
                    continue;
                }
                if (line.Contains('\t'))
                {
                    parseError = $"Tabs are not supported (line {index + 1}).";
                    return false;
                }
                var currentIndent = CountIndent(line);
                if (currentIndent < indent)
                {
                    break;
                }
                if (currentIndent > indent)
                {
                    parseError = $"Invalid indentation (line {index + 1}).";
                    return false;
                }

                var trimmed = line.Trim();
                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    parseError = $"Unexpected list item (line {index + 1}).";
                    return false;
                }

                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex <= 0)
                {
                    parseError = $"Invalid mapping (line {index + 1}).";
                    return false;
                }

                var key = trimmed[..colonIndex].Trim();
                var valuePart = trimmed[(colonIndex + 1)..].TrimStart();
                index++;

                if (valuePart.Length > 0)
                {
                    map[key] = ParseScalar(valuePart);
                    continue;
                }

                var nextIndex = index;
                while (nextIndex < lines.Length && string.IsNullOrWhiteSpace(lines[nextIndex]))
                {
                    nextIndex++;
                }
                if (nextIndex >= lines.Length || CountIndent(lines[nextIndex]) <= indent)
                {
                    map[key] = null;
                    continue;
                }
                if (CountIndent(lines[nextIndex]) < indent + 2)
                {
                    parseError = $"Invalid indentation (line {nextIndex + 1}).";
                    return false;
                }

                if (lines[nextIndex].TrimStart().StartsWith("- ", StringComparison.Ordinal))
                {
                    if (!ParseList(indent + 2, out var list))
                    {
                        return false;
                    }
                    map[key] = list;
                }
                else
                {
                    if (!ParseMap(indent + 2, out var nested))
                    {
                        return false;
                    }
                    map[key] = nested;
                }
            }
            return true;
        }

        bool ParseList(int indent, out List<object?> list)
        {
            list = new List<object?>();
            while (index < lines.Length)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                {
                    index++;
                    continue;
                }
                if (line.Contains('\t'))
                {
                    parseError = $"Tabs are not supported (line {index + 1}).";
                    return false;
                }
                var currentIndent = CountIndent(line);
                if (currentIndent < indent)
                {
                    break;
                }
                if (currentIndent > indent)
                {
                    parseError = $"Invalid indentation (line {index + 1}).";
                    return false;
                }

                var trimmed = line.TrimStart();
                if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    parseError = $"Invalid list entry (line {index + 1}).";
                    return false;
                }
                var valuePart = trimmed[2..].TrimStart();
                list.Add(ParseScalar(valuePart));
                index++;
            }
            return true;
        }

        if (!ParseMap(0, out data))
        {
            error = parseError;
            return false;
        }
        error = parseError;
        return true;
    }

    private static int CountIndent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }
        return count;
    }

    private static object? ParseScalar(string value)
    {
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "~", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (string.Equals(value, "[]", StringComparison.OrdinalIgnoreCase))
        {
            return new List<object?>();
        }
        if (string.Equals(value, "{}", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        if (value.Length >= 2 && value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
        {
            var inner = value[1..^1];
            inner = inner.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
            return inner;
        }
        if (value.Length >= 2 && value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))
        {
            var inner = value[1..^1];
            return inner.Replace("''", "'");
        }

        return value;
    }

    private static string SerializeMap(IDictionary<string, object?> map, int indent)
    {
        var indentText = new string(' ', indent);
        var lines = new List<string>();
        foreach (var (key, value) in map)
        {
            switch (value)
            {
                case Dictionary<string, object?> nested:
                    lines.Add($"{indentText}{key}:");
                    lines.Add(SerializeMap(nested, indent + 2).TrimEnd('\n'));
                    break;
                case Dictionary<object, object> legacyNested:
                    var converted = legacyNested.ToDictionary(
                        kvp => kvp.Key.ToString() ?? string.Empty,
                        kvp => (object?)kvp.Value,
                        StringComparer.OrdinalIgnoreCase);
                    lines.Add($"{indentText}{key}:");
                    lines.Add(SerializeMap(converted, indent + 2).TrimEnd('\n'));
                    break;
                case IEnumerable list when value is not string:
                    var items = list.Cast<object?>().ToList();
                    if (items.Count == 0)
                    {
                        lines.Add($"{indentText}{key}: []");
                    }
                    else
                    {
                        lines.Add($"{indentText}{key}:");
                        lines.Add(SerializeList(items, indent + 2).TrimEnd('\n'));
                    }
                    break;
                default:
                    lines.Add($"{indentText}{key}: {SerializeScalar(value)}");
                    break;
            }
        }
        return string.Join("\n", lines) + "\n";
    }

    private static string SerializeList(IEnumerable list, int indent)
    {
        var indentText = new string(' ', indent);
        var lines = list.Cast<object?>().Select(item => $"{indentText}- {SerializeScalar(item)}").ToList();
        return string.Join("\n", lines) + "\n";
    }

    private static string SerializeScalar(object? value)
    {
        if (value is null)
        {
            return "null";
        }
        if (value is bool boolean)
        {
            return boolean ? "true" : "false";
        }
        if (value is int or long or double or decimal)
        {
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        }

        var text = value.ToString() ?? string.Empty;
        if (text.Length == 0)
        {
            return "\"\"";
        }
        if (NeedsQuoting(text))
        {
            text = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
            return $"\"{text}\"";
        }
        return text;
    }

    private static bool NeedsQuoting(string text)
    {
        if (text.StartsWith(' ') || text.EndsWith(' '))
        {
            return true;
        }
        if (text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }
        return text.IndexOfAny(new[] { ':', '#', '[', ']', '{', '}', ',', '&', '*', '?', '|', '>', '!', '%', '@' }) >= 0;
    }
}
