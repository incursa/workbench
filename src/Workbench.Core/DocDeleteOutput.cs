namespace Workbench
{
    public sealed record DocDeleteOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] DocDeleteData Data);
}
