using System.Text.Json.Serialization;

namespace Workbench;

public sealed record DocLinkOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] DocLinkData Data);
