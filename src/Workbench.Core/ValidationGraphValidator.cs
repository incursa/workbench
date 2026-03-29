namespace Workbench.Core;

internal static partial class ValidationGraphValidator
{
    public static void ValidateCanonicalGraph(
        string repoRoot,
        WorkbenchConfig config,
        ValidationOptions options,
        string selectedProfile,
        List<string> scopePrefixes,
        ArtifactIdPolicy artifactIdPolicy,
        ValidationResult result)
    {
        var docExcludes = NormalizePrefixes(config.Validation?.DocExclude);
        var graph = BuildGraph(repoRoot, config, options, artifactIdPolicy, result, scopePrefixes, docExcludes);

        if (ValidationProfiles.IsEnabledFor(selectedProfile, ValidationProfiles.Traceable))
        {
            EmitDuplicateIdFindings(graph, ValidationProfiles.Traceable, scopePrefixes, result);
            EmitTraceableFindings(graph, ValidationProfiles.Traceable, scopePrefixes, result);
        }

        if (ValidationProfiles.IsEnabledFor(selectedProfile, ValidationProfiles.Auditable))
        {
            EmitAuditableFindings(graph, ValidationProfiles.Auditable, scopePrefixes, result);
        }
    }
}
