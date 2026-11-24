using System.Text.Json;
using System.Text.RegularExpressions;
using Workbench.Core;

#pragma warning disable MA0048 // Provider options and their closed value sets form one configuration contract.

namespace Workbench.SpecAttestProvider;

public enum SpecAttestEndpointMode
{
    Local,
    Hosted
}

public static class SpecAttestAuthorityModes
{
    public const string GitWorkbench = "git-workbench";
    public const string Manager = "manager";
}

public sealed partial record SpecAttestProviderOptions
{
    public required Uri Endpoint { get; init; }

    public required SpecAttestEndpointMode EndpointMode { get; init; }

    public required string SetId { get; init; }

    public required string ManagerProjectId { get; init; }

    public required string AuthorityMode { get; init; }

    public required string AuthorityEpoch { get; init; }

    public required string AuthorityProviderId { get; init; }

    public required ResourceCompatibilityPin StandardPin { get; init; }

    public string ProviderId { get; init; } = SpecAttestResourceProvider.DefaultProviderId;

    public string? AccessToken { get; init; }

    public int MaximumBatchSize { get; init; } = 25;

    public static SpecAttestProviderOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        Dictionary<string, string> values = ParseArguments(arguments);
        HashSet<string> allowed =
        [
            "endpoint",
            "mode",
            "set-id",
            "project-id",
            "authority-mode",
            "authority-epoch",
            "authority-provider-id",
            "standard-pin-file",
            "token-env",
            "provider-id"
        ];
        string? unknown = values.Keys.FirstOrDefault(name => !allowed.Contains(name));
        if (unknown is not null)
        {
            throw new ArgumentException($"Unknown provider argument '--{unknown}'.", nameof(arguments));
        }

        string endpointValue = Required(values, "endpoint");
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out Uri? endpoint))
        {
            throw new ArgumentException("--endpoint must be an absolute URI.", nameof(arguments));
        }

        SpecAttestEndpointMode endpointMode = Required(values, "mode") switch
        {
            "local" => SpecAttestEndpointMode.Local,
            "hosted" => SpecAttestEndpointMode.Hosted,
            _ => throw new ArgumentException("--mode must be 'local' or 'hosted'.", nameof(arguments))
        };
        string pinPath = Path.GetFullPath(Required(values, "standard-pin-file"));
        ResourceCompatibilityPin pin = LoadStandardPin(pinPath);
        string tokenEnvironmentVariable = values.GetValueOrDefault("token-env", "WORKBENCH_SPECATTEST_TOKEN");
        if (!EnvironmentVariableName().IsMatch(tokenEnvironmentVariable))
        {
            throw new ArgumentException(
                "--token-env must name an environment variable without shell syntax.",
                nameof(arguments));
        }

        SpecAttestProviderOptions options = new()
        {
            Endpoint = endpoint,
            EndpointMode = endpointMode,
            SetId = Required(values, "set-id"),
            ManagerProjectId = Required(values, "project-id"),
            AuthorityMode = Required(values, "authority-mode"),
            AuthorityEpoch = Required(values, "authority-epoch"),
            AuthorityProviderId = Required(values, "authority-provider-id"),
            StandardPin = pin,
            ProviderId = values.GetValueOrDefault("provider-id", SpecAttestResourceProvider.DefaultProviderId),
            AccessToken = Environment.GetEnvironmentVariable(tokenEnvironmentVariable)
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        if (!this.Endpoint.IsAbsoluteUri
            || this.Endpoint.UserInfo.Length > 0
            || this.Endpoint.Query.Length > 0
            || this.Endpoint.Fragment.Length > 0)
        {
            throw new InvalidOperationException(
                "SpecAttest endpoint must be an absolute service root without credentials, query, or fragment.");
        }

        bool isHttp = string.Equals(this.Endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        bool isHttps = string.Equals(this.Endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!isHttp && !isHttps)
        {
            throw new InvalidOperationException("SpecAttest endpoint must use HTTP or HTTPS.");
        }

        if (this.EndpointMode == SpecAttestEndpointMode.Local && !this.Endpoint.IsLoopback)
        {
            throw new InvalidOperationException("Local SpecAttest endpoints must use a loopback host.");
        }

        if (this.EndpointMode == SpecAttestEndpointMode.Hosted && !isHttps)
        {
            throw new InvalidOperationException("Hosted SpecAttest endpoints must use HTTPS.");
        }

        if (this.EndpointMode == SpecAttestEndpointMode.Hosted && string.IsNullOrWhiteSpace(this.AccessToken))
        {
            throw new InvalidOperationException(
                "Hosted SpecAttest endpoints require a bearer token supplied through the configured environment variable.");
        }

        if (!Guid.TryParse(this.SetId, out _))
        {
            throw new InvalidOperationException("SpecAttest set ID must be a UUID.");
        }

        if (string.IsNullOrWhiteSpace(this.ManagerProjectId)
            || string.IsNullOrWhiteSpace(this.AuthorityEpoch)
            || string.IsNullOrWhiteSpace(this.AuthorityProviderId)
            || string.IsNullOrWhiteSpace(this.ProviderId))
        {
            throw new InvalidOperationException("Project ID, authority provider ID, authority epoch, and provider ID are required.");
        }

        if (this.AuthorityMode is not (SpecAttestAuthorityModes.GitWorkbench or SpecAttestAuthorityModes.Manager))
        {
            throw new InvalidOperationException("Authority mode must be 'git-workbench' or 'manager'.");
        }

        if (this.MaximumBatchSize <= 0)
        {
            throw new InvalidOperationException("Maximum batch size must be greater than zero.");
        }

        SpecAttestInterchangeValidator.ValidatePin(this.StandardPin);
    }

    private static Dictionary<string, string> ParseArguments(IReadOnlyList<string> arguments)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index += 2)
        {
            string name = arguments[index];
            if (!name.StartsWith("--", StringComparison.Ordinal) || index + 1 >= arguments.Count)
            {
                throw new ArgumentException("Provider arguments must use '--name value' pairs.", nameof(arguments));
            }

            if (!values.TryAdd(name[2..], arguments[index + 1]))
            {
                throw new ArgumentException(
                    $"Provider argument '{name}' was supplied more than once.",
                    nameof(arguments));
            }
        }

        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"--{name} is required.", nameof(values));
    }

    private static ResourceCompatibilityPin LoadStandardPin(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, SpecAttestProviderJsonContext.Default.ResourceCompatibilityPin)
            ?? throw new ArgumentException($"Standard pin file '{path}' is empty or invalid.", nameof(path));
    }

    [GeneratedRegex(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnvironmentVariableName();
}

#pragma warning restore MA0048
