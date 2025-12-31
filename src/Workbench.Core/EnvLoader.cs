namespace Workbench;

public static class EnvLoader
{
    private static readonly HashSet<string> loadedRoots = new(StringComparer.Ordinal);

    public static void LoadRepoEnv(string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return;
        }

        var fullRoot = Path.GetFullPath(repoRoot);
        if (!loadedRoots.Add(fullRoot))
        {
            return;
        }

        LoadEnvFile(Path.Combine(fullRoot, ".env"));
        LoadEnvFile(Path.Combine(fullRoot, ".workbench", "credentials.env"));
    }

    private static void LoadEnvFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line[7..].TrimStart();
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2)
            {
                var first = value[0];
                var last = value[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    value = value[1..^1];
                }
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
