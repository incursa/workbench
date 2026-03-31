using System.Diagnostics;
using System.Reflection;
using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class CueCliTests
{
    [TestMethod]
    public void ResolveCueExecutable_PrefersBundledCueWhenRepoLocalCueIsMissing()
    {
        var originalWorkbenchCue = Environment.GetEnvironmentVariable("WORKBENCH_CUE_BIN");
        var originalSpecTraceCue = Environment.GetEnvironmentVariable("SPEC_TRACE_CUE_BIN");
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-cue-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoRoot);

        try
        {
            Environment.SetEnvironmentVariable("WORKBENCH_CUE_BIN", null);
            Environment.SetEnvironmentVariable("SPEC_TRACE_CUE_BIN", null);

            var bundledCue = InvokeCueCliString("TryResolveBundledCueExecutable");
            Assert.IsFalse(string.IsNullOrWhiteSpace(bundledCue), "Expected a bundled CUE executable for the current platform.");

            var resolvedCue = InvokeCueCliString("ResolveCueExecutable", repoRoot);

            Assert.AreEqual(bundledCue, resolvedCue);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WORKBENCH_CUE_BIN", originalWorkbenchCue);
            Environment.SetEnvironmentVariable("SPEC_TRACE_CUE_BIN", originalSpecTraceCue);

            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, true);
            }
        }
    }

    [TestMethod]
    public void TryResolveBundledCueExecutable_ExtractsRunnableCurrentPlatformBinary()
    {
        var bundledCue = InvokeCueCliString("TryResolveBundledCueExecutable");
        Assert.IsFalse(string.IsNullOrWhiteSpace(bundledCue), "Expected a bundled CUE executable for the current platform.");
        Assert.IsTrue(File.Exists(bundledCue), $"Expected bundled CUE executable at '{bundledCue}'.");

        var startInfo = new ProcessStartInfo
        {
            FileName = bundledCue,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("version");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the bundled CUE executable.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.AreEqual(0, process.ExitCode, $"{standardError}{Environment.NewLine}{standardOutput}".Trim());
        var bundledVersion = InvokeCueCliString("GetBundledCueVersion");
        Assert.IsTrue(
            standardOutput.Contains($"cue version {bundledVersion}", StringComparison.Ordinal),
            $"Expected CUE version '{bundledVersion}' in '{standardOutput}'.");
    }

    private static string InvokeCueCliString(string methodName, params object[]? args)
    {
        var cueCliType = typeof(ValidationService).Assembly.GetType("Workbench.Core.CueCli", throwOnError: true)
            ?? throw new InvalidOperationException("Unable to locate Workbench.Core.CueCli.");
        var method = cueCliType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Unable to locate CueCli.{methodName}.");
        var result = method.Invoke(null, args);
        return result as string ?? throw new InvalidOperationException($"CueCli.{methodName} did not return a string.");
    }
}
