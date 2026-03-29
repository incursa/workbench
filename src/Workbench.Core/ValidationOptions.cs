namespace Workbench.Core;

/// <summary>
/// Per-run validation options supplied by the CLI.
/// </summary>
/// <param name="LinkInclude">Repo-relative prefixes to include for link validation.</param>
/// <param name="LinkExclude">Repo-relative prefixes to exclude for link validation.</param>
/// <param name="SkipDocSchema">When true, skips doc front matter schema validation.</param>
/// <param name="Profile">Validation profile override.</param>
/// <param name="Scope">Repo-relative path prefixes to limit validation scope.</param>
public sealed record ValidationOptions(
    IList<string>? LinkInclude = null,
    IList<string>? LinkExclude = null,
    bool SkipDocSchema = false,
    string? Profile = null,
    IList<string>? Scope = null);
