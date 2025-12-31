namespace Workbench;

public sealed record ValidationOptions(
    IList<string>? LinkInclude = null,
    IList<string>? LinkExclude = null,
    bool SkipDocSchema = false);
