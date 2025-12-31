namespace Workbench;

public sealed record EnvUpdateResult(
    string Path,
    string Key,
    bool Created,
    bool Updated,
    bool Removed);
