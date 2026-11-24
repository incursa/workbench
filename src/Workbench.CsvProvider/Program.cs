using Workbench.Core;
using Workbench.CsvProvider;

CsvProviderOptions options;
try
{
    options = ParseOptions(args);
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
{
    await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
    return 2;
}

CsvResourceProvider provider = new(options);
ResourceProviderProtocolHost host = new(provider);
await host.RunAsync(Console.In, Console.Out).ConfigureAwait(false);
return 0;

static CsvProviderOptions ParseOptions(string[] arguments)
{
    Dictionary<string, string> values = new(StringComparer.Ordinal);
    for (int index = 0; index < arguments.Length; index += 2)
    {
        if (index + 1 >= arguments.Length || !arguments[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("CSV provider arguments must use '--name value' pairs.", nameof(arguments));
        }

        string name = arguments[index][2..];
        if (!values.TryAdd(name, arguments[index + 1]))
        {
            throw new ArgumentException($"CSV provider argument '--{name}' was supplied more than once.", nameof(arguments));
        }
    }

    HashSet<string> allowed =
    [
        "root",
        "resource-set",
        "authority-epoch",
        "role",
        "provider-id",
        "provider-version",
        "authority-provider-id"
    ];
    string? unknown = values.Keys.FirstOrDefault(name => !allowed.Contains(name));
    if (unknown is not null)
    {
        throw new ArgumentException($"Unknown CSV provider argument '--{unknown}'.", nameof(arguments));
    }

    string Required(string name)
    {
        return values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Missing required CSV provider argument '--{name}'.");
    }

    ResourceProviderRole role = Required("role") switch
    {
        "authority" => ResourceProviderRole.Authority,
        "projection" => ResourceProviderRole.ReadOnlyEnricher,
        _ => throw new ArgumentException("CSV provider '--role' must be 'authority' or 'projection'.", nameof(arguments))
    };
    string providerId = values.GetValueOrDefault("provider-id", CsvProviderOptions.DefaultProviderId);
    string? authorityProviderId = values.TryGetValue("authority-provider-id", out string? configuredAuthority)
        && !string.IsNullOrWhiteSpace(configuredAuthority)
            ? configuredAuthority
            : null;
    if (authorityProviderId is null)
    {
        authorityProviderId = role == ResourceProviderRole.Authority
            ? providerId
            : throw new InvalidOperationException(
                "CSV projection mode requires '--authority-provider-id' naming the external authority.");
    }
    return new CsvProviderOptions
    {
        RootDirectory = Required("root"),
        ResourceSet = Required("resource-set"),
        ProviderId = providerId,
        ProviderVersion = values.GetValueOrDefault("provider-version", CsvProviderOptions.CurrentProviderVersion),
        Role = role,
        AuthorityProviderId = authorityProviderId,
        AuthorityEpoch = Required("authority-epoch")
    };
}
