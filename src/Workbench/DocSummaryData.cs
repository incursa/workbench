using System.Text.Json.Serialization;

namespace Workbench
{
    public sealed record DocSummaryData(
        [property: JsonPropertyName("filesUpdated")] int FilesUpdated,
        [property: JsonPropertyName("notesAdded")] int NotesAdded,
        [property: JsonPropertyName("updatedFiles")] IList<string> UpdatedFiles,
        [property: JsonPropertyName("skippedFiles")] IList<string> SkippedFiles,
        [property: JsonPropertyName("errors")] IList<string> Errors,
        [property: JsonPropertyName("warnings")] IList<string> Warnings);
}
