namespace Workbench
{
    public sealed record ItemListOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ItemListData Data);
}