using System.Diagnostics;
using System.Text.Json;
using Xunit.Sdk;

namespace Workbench.IntegrationTests;

internal sealed record CommandResult(int ExitCode, string StdOut, string StdErr);

internal static class ProcessRunner
{
    public static CommandResult Run(string workingDirectory, string fileName, params string[] args)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, stdout.Trim(), stderr.Trim());
    }
}

internal sealed class TempRepo : IDisposable
{
    public TempRepo(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempRepo Create()
    {
        var repoRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);
        return new TempRepo(repoRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

internal static class WorkbenchCli
{
    private static readonly Lazy<string> DllPath = new(BuildWorkbenchCli);

    public static CommandResult Run(string workingDirectory, params string[] args)
    {
        var dllPath = DllPath.Value;
        var allArgs = new List<string> { dllPath };
        allArgs.AddRange(args);
        return ProcessRunner.Run(workingDirectory, "dotnet", allArgs.ToArray());
    }

    private static string BuildWorkbenchCli()
    {
        var repoRoot = FindRepoRoot();
        var projectPath = System.IO.Path.Combine(repoRoot, "src", "Workbench", "Workbench.csproj");
        var buildResult = ProcessRunner.Run(repoRoot, "dotnet", "build", projectPath, "-c", "Debug");
        if (buildResult.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet build failed: {buildResult.StdErr}\n{buildResult.StdOut}");
        }

        var dllPath = System.IO.Path.Combine(repoRoot, "src", "Workbench", "bin", "Debug", "net10.0", "Workbench.dll");
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"Workbench CLI not found at {dllPath}.");
        }
        return dllPath;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "Workbench.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Workbench.slnx to locate repo root.");
    }
}

internal static class TestAssertions
{
    public static JsonElement ParseJson(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    public static void RequireGhTestsEnabled()
    {
        var enabled = Environment.GetEnvironmentVariable("WORKBENCH_RUN_GH_TESTS");
        if (!string.Equals(enabled, "1", StringComparison.OrdinalIgnoreCase))
        {
            throw new SkipException("Set WORKBENCH_RUN_GH_TESTS=1 to enable GitHub CLI integration tests.");
        }
    }
}
