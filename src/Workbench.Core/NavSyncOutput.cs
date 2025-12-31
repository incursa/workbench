using System.Text.Json.Serialization;

namespace Workbench
{
    public sealed record NavSyncOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] NavSyncData Data);
}
