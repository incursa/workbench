namespace Workbench.Core;

/// <summary>
/// JSON response envelope for doc show output.
/// </summary>
/// <param name="Ok">True when the document was resolved.</param>
/// <param name="Data">Document payload.</param>
public sealed record DocShowOutput(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("data")] DocShowData Data);
