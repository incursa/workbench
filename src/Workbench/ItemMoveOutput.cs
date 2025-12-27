namespace Workbench
{
    public sealed record ItemMoveOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ItemMoveData Data);
}