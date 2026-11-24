using System.Diagnostics;
using System.Text;

namespace Workbench.Core;

/// <summary>
/// NDJSON stdin/stdout transport for a provider executable. It uses no shell and never loads provider assemblies.
/// </summary>
public sealed class ProviderProcessTransport : IResourceProviderTransport
{
    private const int MaximumStderrCharacters = 32 * 1024;
    private readonly ResourceProviderProcessConfig configuration;
    private readonly string repoRoot;
    private readonly TimeSpan requestTimeout;
    private readonly SemaphoreSlim requestGate = new(1, 1);
    private readonly StringBuilder stderr = new();
    private Process? process;
    private StreamWriter? input;
    private StreamReader? output;
    private Task? stderrPump;
    private bool disposed;

    public ProviderProcessTransport(
        ResourceProviderProcessConfig configuration,
        string repoRoot,
        TimeSpan requestTimeout)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(configuration.Executable))
        {
            throw new ArgumentException("Provider executable is required.", nameof(configuration));
        }

        if (requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout), "Request timeout must be greater than zero.");
        }

        this.configuration = configuration;
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.requestTimeout = requestTimeout;
    }

    public async Task<ProviderResponseEnvelope> SendAsync(
        ProviderRequestEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        await this.requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.EnsureStarted();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(this.requestTimeout);

            var json = JsonSerializer.Serialize(request, ProviderProtocolJsonContext.Default.ProviderRequestEnvelope);
            try
            {
                await this.input!.WriteLineAsync(json.AsMemory(), timeout.Token).ConfigureAwait(false);
                await this.input.FlushAsync(timeout.Token).ConfigureAwait(false);
                var responseLine = await this.output!.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (responseLine is null)
                {
                    throw CreateTransportFailure("Provider closed stdout before returning a response.");
                }

                return JsonSerializer.Deserialize(
                    responseLine,
                    ProviderProtocolJsonContext.Default.ProviderResponseEnvelope)
                    ?? throw CreateTransportFailure("Provider returned an empty response envelope.");
            }
            catch (OperationCanceledException)
            {
                this.TerminateProcess();
                throw;
            }
            catch (JsonException exception)
            {
                this.TerminateProcess();
                throw CreateTransportFailure($"Provider returned invalid JSON: {exception.Message}");
            }
            catch (IOException exception)
            {
                this.TerminateProcess();
                throw CreateTransportFailure($"Provider transport failed: {exception.Message}");
            }
        }
        finally
        {
            this.requestGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        await this.requestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            this.TerminateProcess();
            if (this.stderrPump is not null)
            {
#pragma warning disable VSTHRD003 // The pump is created and owned by this transport instance.
                await this.stderrPump.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }

            if (this.input is not null)
            {
                await this.input.DisposeAsync().ConfigureAwait(false);
            }

            if (this.output is not null)
            {
                this.output.Dispose();
            }
            this.process?.Dispose();
        }
        finally
        {
            this.requestGate.Release();
            this.requestGate.Dispose();
        }
    }

    private void EnsureStarted()
    {
        if (this.process is { HasExited: false })
        {
            return;
        }

        if (this.process is { HasExited: true })
        {
            throw CreateTransportFailure($"Provider exited with code {this.process.ExitCode}.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = this.configuration.Executable,
            WorkingDirectory = ResolveWorkingDirectory(this.repoRoot, this.configuration.WorkingDirectory),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in this.configuration.Arguments ?? [])
        {
            startInfo.ArgumentList.Add(argument);
        }

        this.process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        try
        {
            if (!this.process.Start())
            {
                throw CreateTransportFailure("Provider process did not start.");
            }
        }
        catch (Exception exception) when (exception is not ResourceProviderProtocolException)
        {
            throw CreateTransportFailure(
                $"Failed to start provider '{this.configuration.Id}' ({exception.GetType().Name}): {exception.Message}");
        }

        this.input = this.process.StandardInput;
        this.output = this.process.StandardOutput;
        this.stderrPump = this.PumpStderrAsync(this.process.StandardError);
    }

    private async Task PumpStderrAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            lock (this.stderr)
            {
                if (this.stderr.Length >= MaximumStderrCharacters)
                {
                    continue;
                }

                var remaining = MaximumStderrCharacters - this.stderr.Length;
                this.stderr.AppendLine(line.Length <= remaining ? line : line[..remaining]);
            }
        }
    }

    private void TerminateProcess()
    {
        if (this.process is not { HasExited: false })
        {
            return;
        }

        try
        {
            this.process.Kill(entireProcessTree: true);
            this.process.WaitForExit((int)Math.Min(this.requestTimeout.TotalMilliseconds, 5000));
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and the kill request.
        }
    }

    private ResourceProviderProtocolException CreateTransportFailure(string message)
    {
        string detail;
        lock (this.stderr)
        {
            detail = this.stderr.ToString().Trim();
        }

        var fullMessage = string.IsNullOrWhiteSpace(detail) ? message : $"{message} Provider stderr: {detail}";
        return new ResourceProviderProtocolException(
            new ProviderError("provider.transport.failure", fullMessage, Retryable: true));
    }

    private static string ResolveWorkingDirectory(string repoRoot, string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return repoRoot;
        }

        return Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(repoRoot, configured));
    }
}
