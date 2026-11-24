using Workbench.Core;
using Workbench.SpecAttestProvider;

try
{
    SpecAttestProviderOptions options = SpecAttestProviderOptions.Parse(args);
    using HttpClient httpClient = new();
    SpecAttestManagerClient manager = new(httpClient, options);
    SpecAttestResourceProvider provider = new(manager, options);
    ResourceProviderProtocolHost host = new(provider);
    await host.RunAsync(Console.In, Console.Out).ConfigureAwait(false);
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or JsonException)
{
    await Console.Error.WriteLineAsync($"SpecAttest provider configuration failed: {exception.Message}").ConfigureAwait(false);
    return 2;
}
