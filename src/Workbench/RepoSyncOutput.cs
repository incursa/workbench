using System.Text.Json.Serialization;

namespace Workbench;

public sealed record RepoSyncOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] RepoSyncData Data);
