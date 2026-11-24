#pragma warning disable MA0048 // Provider configuration and its mode form one public configuration surface.
#pragma warning disable S1075 // The official ClickUp API endpoint is an intentional provider default.

namespace Workbench.Providers.ClickUp;

public enum ClickUpProviderMode
{
    ReadOnly,
    Plan,
    Apply
}

/// <summary>Explicit, non-secret configuration for one ClickUp provider process.</summary>
public sealed record ClickUpProviderOptions
{
    public const string DefaultTokenEnvironmentVariable = "CLICKUP_API_TOKEN";

    public required string ListId { get; init; }

    public required string AuthorityEpoch { get; init; }

    public ClickUpProviderMode Mode { get; init; } = ClickUpProviderMode.ReadOnly;

    public string TokenEnvironmentVariable { get; init; } = DefaultTokenEnvironmentVariable;

    public string? StateDirectory { get; init; }

    public string? LocalReferenceCustomFieldId { get; init; }

    public Uri ApiBaseUri { get; init; } = new("https://api.clickup.com/api/v2/");

    public TimeSpan PlanLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public int MaximumHttpAttempts { get; init; } = 3;

    public static ClickUpProviderOptions Parse(IReadOnlyList<string> arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index += 2)
        {
            if (index + 1 >= arguments.Count || !arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Provider arguments must be supplied as '--name value' pairs.");
            }

            if (!values.TryAdd(arguments[index], arguments[index + 1]))
            {
                throw new InvalidOperationException($"Provider argument '{arguments[index]}' was supplied more than once.");
            }
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "--list-id",
            "--authority-epoch",
            "--mode",
            "--token-env",
            "--state-directory",
            "--local-reference-field-id",
            "--api-base-uri"
        };
        var unknown = values.Keys.FirstOrDefault(key => !allowed.Contains(key));
        if (unknown is not null)
        {
            throw new InvalidOperationException($"Unknown provider argument '{unknown}'.");
        }

        var modeText = values.GetValueOrDefault("--mode") ?? "read-only";
        var mode = modeText switch
        {
            "read-only" => ClickUpProviderMode.ReadOnly,
            "plan" => ClickUpProviderMode.Plan,
            "apply" => ClickUpProviderMode.Apply,
            _ => throw new InvalidOperationException("--mode must be one of: read-only, plan, apply.")
        };
        var stateDirectory = NullIfWhiteSpace(values.GetValueOrDefault("--state-directory"));
        if (mode != ClickUpProviderMode.ReadOnly && stateDirectory is null)
        {
            throw new InvalidOperationException("--state-directory is required in plan and apply modes.");
        }

        var tokenEnvironmentVariable = values.GetValueOrDefault("--token-env") ?? DefaultTokenEnvironmentVariable;
        if (string.IsNullOrWhiteSpace(tokenEnvironmentVariable)
            || tokenEnvironmentVariable.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
        {
            throw new InvalidOperationException("--token-env must name an environment variable using letters, digits, and underscores.");
        }

        var baseUri = values.TryGetValue("--api-base-uri", out var baseUriText)
            ? new Uri(baseUriText, UriKind.Absolute)
            : new Uri("https://api.clickup.com/api/v2/");
        if (baseUri.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("--api-base-uri must use http or https.");
        }

        var isOfficialEndpoint = string.Equals(baseUri.Scheme, "https", StringComparison.Ordinal)
            && string.Equals(baseUri.Host, "api.clickup.com", StringComparison.OrdinalIgnoreCase)
            && baseUri.IsDefaultPort;
        if (!isOfficialEndpoint && !baseUri.IsLoopback)
        {
            throw new InvalidOperationException(
                "--api-base-uri must use the official ClickUp endpoint or a loopback mock endpoint.");
        }

        return new ClickUpProviderOptions
        {
            ListId = Require(values, "--list-id"),
            AuthorityEpoch = Require(values, "--authority-epoch"),
            Mode = mode,
            TokenEnvironmentVariable = tokenEnvironmentVariable,
            StateDirectory = stateDirectory is null ? null : Path.GetFullPath(stateDirectory),
            LocalReferenceCustomFieldId = NullIfWhiteSpace(values.GetValueOrDefault("--local-reference-field-id")),
            ApiBaseUri = EnsureTrailingSlash(baseUri)
        };
    }

    public string ResolveToken()
    {
        return Environment.GetEnvironmentVariable(this.TokenEnvironmentVariable)
            is { Length: > 0 } token
            ? token
            : throw new InvalidOperationException(
                $"ClickUp credentials are unavailable. Set the environment variable named by --token-env ({this.TokenEnvironmentVariable}).");
    }

    private static string Require(IReadOnlyDictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{name} is required.");
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
    }
}

#pragma warning restore S1075
#pragma warning restore MA0048
