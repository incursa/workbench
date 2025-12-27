namespace Workbench;

public sealed record RelatedLinks(
    IList<string> Specs,
    IList<string> Adrs,
    IList<string> Files,
    IList<string> Prs,
    IList<string> Issues,
    IList<string> Branches);
