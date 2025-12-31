namespace Workbench
{
    public sealed record ConfigOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ConfigData Data);
}
