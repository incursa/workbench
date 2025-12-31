namespace Workbench;

public static class EnvFileService
{
    public static EnvUpdateResult SetValue(string path, string key, string value)
    {
        return Update(path, key, value, remove: false);
    }

    public static EnvUpdateResult UnsetValue(string path, string key)
    {
        return Update(path, key, value: string.Empty, remove: true);
    }

    private static EnvUpdateResult Update(string path, string key, string value, bool remove)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Env key is required.");
        }

        var existed = File.Exists(path);
        var lines = existed ? File.ReadAllLines(path).ToList() : new List<string>();
        if (remove && !existed)
        {
            return new EnvUpdateResult(path, key, false, false, false);
        }
        var updated = false;
        var removed = false;
        var found = false;
        var output = new List<string>(lines.Count);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                output.Add(line);
                continue;
            }

            var hasExport = false;
            if (trimmed.StartsWith("export ", StringComparison.Ordinal))
            {
                hasExport = true;
                trimmed = trimmed[7..].TrimStart();
            }

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
            {
                output.Add(line);
                continue;
            }

            var existingKey = trimmed[..separator].Trim();
            if (!existingKey.Equals(key, StringComparison.Ordinal))
            {
                output.Add(line);
                continue;
            }

            found = true;
            if (remove)
            {
                removed = true;
                continue;
            }

            var updatedLine = $"{(hasExport ? "export " : string.Empty)}{key}={value}";
            if (!string.Equals(line, updatedLine, StringComparison.Ordinal))
            {
                updated = true;
                output.Add(updatedLine);
                continue;
            }

            output.Add(line);
        }

        if (!found && !remove)
        {
            if (!existed)
            {
                output.Add("# Workbench credentials");
            }
            output.Add($"{key}={value}");
            updated = true;
        }

        if (!updated && !removed)
        {
            return new EnvUpdateResult(path, key, false, false, false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllLines(path, output);

        return new EnvUpdateResult(path, key, !existed, updated, removed);
    }
}
