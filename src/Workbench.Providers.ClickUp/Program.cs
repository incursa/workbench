using Workbench.Core;
using Workbench.Providers.ClickUp;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] arguments)
{
    try
    {
        var options = ClickUpProviderOptions.Parse(arguments);
        var token = options.ResolveToken();
        using var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var provider = new ClickUpResourceProvider(options, httpClient, token);
        var host = new ResourceProviderProtocolHost(provider);
        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        await host.RunAsync(Console.In, Console.Out, shutdown.Token).ConfigureAwait(false);
        return 0;
    }
    catch (OperationCanceledException)
    {
        return 0;
    }
#pragma warning disable EPC12 // Startup diagnostics intentionally exclude stack traces and potential credential-bearing state.
    catch (Exception exception)
    {
        // stderr is operational-only; never include credential values or HTTP payloads.
        await Console.Error.WriteLineAsync($"ClickUp provider could not start ({exception.GetType().Name}): {exception.Message}")
            .ConfigureAwait(false);
        return 2;
    }
#pragma warning restore EPC12
}
