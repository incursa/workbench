using System.Text.Json.Serialization;

namespace Workbench;

public sealed record RepoSyncData(
    [property: JsonPropertyName("items")] ItemSyncData? Items,
    [property: JsonPropertyName("docs")] DocSyncData? Docs,
    [property: JsonPropertyName("nav")] NavSyncData? Nav,
    [property: JsonPropertyName("dryRun")] bool DryRun);
