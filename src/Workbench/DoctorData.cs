namespace Workbench
{
    public sealed record DoctorData(
        [property: JsonPropertyName("repoRoot")] string RepoRoot,
        [property: JsonPropertyName("checks")] IList<DoctorCheck> Checks);
}
