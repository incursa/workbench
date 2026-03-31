using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Workbench.Core;

internal static class CueCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lock ExtractionGate = new();
    private const string BundledCueVersionResourceSuffix = "Tooling.Cue.version.txt";

    public static CueArtifactModel ExportArtifact(string repoRoot, string cuePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveCueExecutable(repoRoot),
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("export");
        startInfo.ArgumentList.Add(cuePath);
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add("artifact");
        startInfo.ArgumentList.Add("--out");
        startInfo.ArgumentList.Add("json");

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"cue export failed for '{cuePath}': {standardError.Trim()}");
        }

        var artifact = JsonSerializer.Deserialize<CueArtifactModel>(standardOutput, JsonOptions);
        return artifact ?? throw new InvalidOperationException($"cue export returned no artifact payload for '{cuePath}'.");
    }

    public static string ResolveCueExecutable(string repoRoot)
    {
        foreach (var envVar in new[] { "WORKBENCH_CUE_BIN", "SPEC_TRACE_CUE_BIN" })
        {
            var envOverride = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(envOverride) && File.Exists(envOverride))
            {
                return envOverride;
            }
        }

        var repoLocal = Path.Combine(repoRoot, ".tools", "cue", "bin", OperatingSystem.IsWindows() ? "cue.exe" : "cue");
        if (File.Exists(repoLocal))
        {
            return repoLocal;
        }

        var bundledCue = TryResolveBundledCueExecutable();
        if (!string.IsNullOrWhiteSpace(bundledCue))
        {
            return bundledCue;
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var goBin = Path.Combine(homeDirectory, "go", "bin", OperatingSystem.IsWindows() ? "cue.exe" : "cue");
        if (File.Exists(goBin))
        {
            return goBin;
        }

        return "cue";
    }

    internal static string GetBundledCueVersion()
    {
        return ReadBundledCueVersion(typeof(CueCli).Assembly);
    }

    internal static string? TryResolveBundledCueExecutable()
    {
        var assembly = typeof(CueCli).Assembly;
        var version = ReadBundledCueVersion(assembly);
        var rid = GetCurrentRid();
        var fileName = OperatingSystem.IsWindows() ? "cue.exe" : "cue";
        var resourceSuffix = $"Tooling.Cue.runtimes.{rid.Replace('-', '_')}.native.{fileName}";
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return null;
        }

        var destinationPath = GetBundledCueExtractionPath(version, rid, fileName);
        if (File.Exists(destinationPath))
        {
            return destinationPath;
        }

        lock (ExtractionGate)
        {
            if (File.Exists(destinationPath))
            {
                return destinationPath;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var tempPath = destinationPath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Unable to open bundled CUE resource '{resourceName}'.");
            using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.CopyTo(destination);
                destination.Flush(true);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    tempPath,
                    UnixFileMode.UserRead |
                    UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead |
                    UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead |
                    UnixFileMode.OtherExecute);
            }

            File.Move(tempPath, destinationPath, true);
        }

        return destinationPath;
    }

    private static string ReadBundledCueVersion(Assembly assembly)
    {
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(BundledCueVersionResourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unable to locate the bundled CUE version resource.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Unable to open the bundled CUE version resource '{resourceName}'.");
        using var reader = new StreamReader(stream);
        var version = reader.ReadToEnd().Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("The bundled CUE version resource is empty.");
        }

        return version;
    }

    private static string GetBundledCueExtractionPath(string version, string rid, string fileName)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Path.GetTempPath(), "Incursa", "Workbench");
        }

        return Path.Combine(root, "Incursa", "Workbench", "tools", "cue", version, rid, fileName);
    }

    private static string GetCurrentRid()
    {
        string os;
        if (OperatingSystem.IsWindows())
        {
            os = "win";
        }
        else if (OperatingSystem.IsMacOS())
        {
            os = "osx";
        }
        else if (OperatingSystem.IsLinux())
        {
            os = "linux";
        }
        else
        {
            throw new PlatformNotSupportedException("CUE bundling is only supported on Windows, macOS, and Linux.");
        }

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"CUE bundling is not supported on architecture '{RuntimeInformation.OSArchitecture}'."),
        };

        return $"{os}-{arch}";
    }
}
