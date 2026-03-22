namespace Workbench.Core;

/// <summary>
/// Related links stored in work item front matter.
/// </summary>
/// <param name="Specs">Spec document links.</param>
/// <param name="Files">Code or file links.</param>
/// <param name="Prs">Pull request URLs.</param>
/// <param name="Issues">Issue URLs or references.</param>
/// <param name="Branches">Git branch names.</param>
public sealed record RelatedLinks(
    IList<string> Specs,
    IList<string> Files,
    IList<string> Prs,
    IList<string> Issues,
    IList<string> Branches);
