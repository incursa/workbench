using Workbench.Core;
using Workbench.SpecAttestProvider;

try
{
    if (args.Length > 0 && string.Equals(args[0], "verify-edition", StringComparison.Ordinal))
    {
        if (args.Length != 3)
        {
            throw new ArgumentException("Usage: workbench-specattest-provider verify-edition <repository-root> <manifest-path>", nameof(args));
        }

        SpecAttestEditionVerificationReport report = SpecAttestEditionVerifier.Verify(args[1], args[2], SpecAttestInterchangeContract.StandardPin());
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(report)).ConfigureAwait(false);
        return 0;
    }

    if (args.Length > 0 && string.Equals(args[0], "verify-corpus", StringComparison.Ordinal))
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Usage: workbench-specattest-provider verify-corpus <corpus-directory>", nameof(args));
        }

        SpecAttestConformanceReport report = SpecAttestConformanceCorpusRunner.Run(args[1]);
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(report)).ConfigureAwait(false);
        return 0;
    }

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
