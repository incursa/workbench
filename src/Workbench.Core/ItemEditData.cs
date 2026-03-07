namespace Workbench.Core;

/// <summary>
/// Payload for structured work item edit operations.
/// </summary>
/// <param name="Item">Updated work item payload.</param>
/// <param name="PathChanged">True when the item file was renamed.</param>
/// <param name="TitleUpdated">True when the title/front matter heading changed.</param>
/// <param name="SummaryUpdated">True when the Summary section changed.</param>
/// <param name="AcceptanceCriteriaUpdated">True when the Acceptance criteria section changed.</param>
/// <param name="NotesAppended">True when a note was appended.</param>
public sealed record ItemEditData(
    [property: JsonPropertyName("item")] WorkItemPayload Item,
    [property: JsonPropertyName("pathChanged")] bool PathChanged,
    [property: JsonPropertyName("titleUpdated")] bool TitleUpdated,
    [property: JsonPropertyName("summaryUpdated")] bool SummaryUpdated,
    [property: JsonPropertyName("acceptanceCriteriaUpdated")] bool AcceptanceCriteriaUpdated,
    [property: JsonPropertyName("notesAppended")] bool NotesAppended);
