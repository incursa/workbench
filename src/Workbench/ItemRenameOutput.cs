namespace Workbench
{
    public sealed record ItemRenameOutput(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] ItemRenameData Data);
}