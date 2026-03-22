using System.Text;
using System.Text.RegularExpressions;

namespace Workbench.Core;

public sealed record ArtifactIdPolicy(int MinimumDigits, IReadOnlyDictionary<string, string> Templates)
{
    private const string PolicyFileName = "artifact-id-policy.json";

    public static ArtifactIdPolicy Default { get; } = new(
        4,
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["specification"] = "SPEC-{domain}{grouping}",
            ["architecture"] = "ARC-{domain}{grouping}-{sequence}",
            ["work_item"] = "WI-{domain}{grouping}-{sequence}",
            ["verification"] = "VER-{domain}{grouping}-{sequence}"
        });

    public static ArtifactIdPolicy Load(string repoRoot)
    {
        return Load(repoRoot, out _);
    }

    public static ArtifactIdPolicy Load(string repoRoot, out string? error)
    {
        error = null;
        var policyPath = Path.Combine(repoRoot, PolicyFileName);
        if (!File.Exists(policyPath))
        {
            return Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(policyPath));
            var root = document.RootElement;

            var minimumDigits = Default.MinimumDigits;
            if (root.TryGetProperty("sequence", out var sequence) &&
                sequence.ValueKind == JsonValueKind.Object &&
                sequence.TryGetProperty("minimum_digits", out var digits) &&
                digits.TryGetInt32(out var parsedDigits) &&
                parsedDigits > 0)
            {
                minimumDigits = parsedDigits;
            }

            var templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("artifact_id_templates", out var templateMap) &&
                templateMap.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in templateMap.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = entry.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            templates[entry.Name] = value;
                        }
                    }
                }
            }

            if (templates.Count == 0)
            {
                error = $"{policyPath}: no artifact_id_templates entries were found.";
                return Default;
            }

            return new ArtifactIdPolicy(minimumDigits, templates);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return Default;
        }
    }

    public string? GetTemplateForDocType(string docType)
    {
        var key = GetTemplateKey(docType);
        if (key is null)
        {
            return null;
        }

        if (Templates.TryGetValue(key, out var template) && !string.IsNullOrWhiteSpace(template))
        {
            return template;
        }

        return Default.Templates.TryGetValue(key, out var fallbackTemplate) ? fallbackTemplate : null;
    }

    public static string? GetTemplateKey(string docType)
    {
        return docType.Trim().ToLowerInvariant() switch
        {
            "spec" or "specification" => "specification",
            "architecture" => "architecture",
            "work_item" or "work-item" => "work_item",
            "verification" => "verification",
            _ => null
        };
    }

    public string BuildArtifactId(
        string docType,
        string? domain,
        string? capability,
        int sequence)
    {
        var template = GetTemplateForDocType(docType);
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException($"No artifact ID template configured for '{docType}'.");
        }

        var id = template
            .Replace("{domain}", NormalizeToken(domain), StringComparison.OrdinalIgnoreCase)
            .Replace("{grouping}", NormalizeGrouping(capability), StringComparison.OrdinalIgnoreCase)
            .Replace("{sequence}", sequence.ToString($"D{MinimumDigits}", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

        return id;
    }

    public string BuildArtifactIdPrefix(string docType, string? domain, string? capability)
    {
        var template = GetTemplateForDocType(docType);
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException($"No artifact ID template configured for '{docType}'.");
        }

        return template
            .Replace("{domain}", NormalizeToken(domain), StringComparison.OrdinalIgnoreCase)
            .Replace("{grouping}", NormalizeGrouping(capability), StringComparison.OrdinalIgnoreCase)
            .Replace("{sequence}", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesArtifactId(
        string docType,
        string artifactId,
        string? domain,
        string? capability)
    {
        var template = GetTemplateForDocType(docType);
        if (string.IsNullOrWhiteSpace(template))
        {
            return true;
        }

        var pattern = BuildArtifactIdPattern(template);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));
        var match = regex.Match(artifactId.Trim());
        if (!match.Success)
        {
            return false;
        }

        var requiresDomain = template.Contains("{domain}", StringComparison.OrdinalIgnoreCase);
        if (requiresDomain)
        {
            var expectedDomain = NormalizeToken(domain);
            if (string.IsNullOrWhiteSpace(expectedDomain))
            {
                return false;
            }

            var matchedDomain = NormalizeToken(match.Groups["domain"].Value);
            if (!string.Equals(matchedDomain, expectedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var requiresGrouping = template.Contains("{grouping}", StringComparison.OrdinalIgnoreCase);
        if (requiresGrouping)
        {
            var expectedGrouping = NormalizeGrouping(capability);
            var matchedGrouping = NormalizeGrouping(match.Groups["grouping"].Value);
            if (string.IsNullOrWhiteSpace(expectedGrouping))
            {
                if (!string.IsNullOrWhiteSpace(matchedGrouping))
                {
                    return false;
                }
            }
            else if (!string.Equals(matchedGrouping, expectedGrouping, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private string BuildArtifactIdPattern(string template)
    {
        var escaped = Regex.Escape(template);
        escaped = escaped.Replace(@"\{domain\}", @"(?<domain>[A-Z][A-Z0-9]*(?:-[A-Z][A-Z0-9]*)*)", StringComparison.OrdinalIgnoreCase);
        escaped = escaped.Replace(@"\{grouping\}", @"(?<grouping>(?:-[A-Z][A-Z0-9]*)*)", StringComparison.OrdinalIgnoreCase);
        escaped = escaped.Replace(@"\{sequence\}", $"(?<sequence>\\d{{{MinimumDigits},}})", StringComparison.OrdinalIgnoreCase);
        return $"^{escaped}$";
    }

    public static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var input = value.Trim().ToUpperInvariant();
        var builder = new StringBuilder(input.Length);
        var needsSeparator = false;

        foreach (var ch in input)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (needsSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(ch);
                needsSeparator = false;
            }
            else if (builder.Length > 0)
            {
                needsSeparator = true;
            }
        }

        return builder.ToString();
    }

    public static string NormalizeGrouping(string? value)
    {
        var normalized = NormalizeToken(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return "-" + normalized;
    }

    public static bool TryParseSequence(string candidate, out int sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return int.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out sequence);
    }
}
