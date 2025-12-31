namespace Workbench
{
    public sealed record BoardOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] BoardData Data);
}
