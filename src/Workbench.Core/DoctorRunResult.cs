namespace Workbench.Core;

/// <summary>Doctor result plus exit-code classification.</summary>
public sealed record DoctorRunResult(
    DoctorData Data,
    bool HasErrors,
    bool HasWarnings);
