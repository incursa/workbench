using System.Text.Json.Serialization;

namespace Workbench
{
    public sealed record DocSummaryOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] DocSummaryData Data);
}
