namespace Workbench.Core;

/// <summary>
/// Canonical validation categories used by structured findings.
/// </summary>
internal static class ValidationCategories
{
    public const string Profile = "profile";
    public const string Schema = "schema";
    public const string Keyword = "keyword";
    public const string Placement = "placement";
    public const string Identifier = "identifier";
    public const string DuplicateId = "duplicate-id";
    public const string UnresolvedReference = "unresolved-reference";
    public const string DownstreamMissing = "downstream-missing";
    public const string VerificationMissing = "verification-missing";
    public const string ReciprocalMismatch = "reciprocal-mismatch";
    public const string OrphanArtifact = "orphan-artifact";
    public const string BodyMismatch = "body-mismatch";
    public const string Scope = "scope";
    public const string RepoState = "repo-state";
}
