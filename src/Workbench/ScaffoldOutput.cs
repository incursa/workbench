namespace Workbench
{
    public sealed record ScaffoldOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ScaffoldData Data);
}