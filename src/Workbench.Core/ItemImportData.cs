namespace Workbench;

public sealed record ItemImportData(
    [property: JsonPropertyName("items")] IList<ItemImportEntry> Items);
