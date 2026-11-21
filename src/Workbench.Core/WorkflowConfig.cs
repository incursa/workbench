namespace Workbench.Core;

#pragma warning disable MA0048

/// <summary>
/// Repository workflow policy. The level controls recommended ceremony without
/// changing the canonical artifact format.
/// </summary>
public sealed record WorkflowConfig
{
    /// <summary>Workflow level: light, standard, or governed.</summary>
    public string Level { get; init; } = WorkflowPolicyResolver.Standard;
}

/// <summary>Resolves workflow levels into explicit, informational policy.</summary>
public static class WorkflowPolicyResolver
{
    public const string Light = "light";
    public const string Standard = "standard";
    public const string Governed = "governed";

    public static WorkflowPolicy Resolve(WorkflowConfig? workflow, ValidationConfig? validation)
    {
        var level = string.IsNullOrWhiteSpace(workflow?.Level)
            ? Standard
            : workflow.Level.Trim().ToLowerInvariant();

        if (level is not (Light or Standard or Governed))
        {
            throw new InvalidOperationException(
                $"Unknown workflow level '{workflow?.Level}'. Expected light, standard, or governed.");
        }

        var configuredProfile = ValidationProfiles.NormalizeOrDefault(validation?.Profile);
        return level switch
        {
            Light => new WorkflowPolicy(
                Light,
                ValidationProfiles.Core,
                WorkItemRequired: false,
                TraceRequired: false,
                "Low-ceremony workflow for trivial changes and emergency fixes."),
            Governed => new WorkflowPolicy(
                Governed,
                string.IsNullOrWhiteSpace(validation?.Profile) ? ValidationProfiles.Auditable : configuredProfile,
                WorkItemRequired: true,
                TraceRequired: true,
                "Governed workflow with explicit work, traceability, and verification evidence."),
            _ => new WorkflowPolicy(
                Standard,
                configuredProfile,
                WorkItemRequired: false,
                TraceRequired: false,
                "Standard workflow; work items are expected for meaningful changes but optional for trivial work.")
        };
    }
}

/// <summary>Resolved workflow semantics returned to commands and agents.</summary>
public sealed record WorkflowPolicy(
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("validationProfile")] string ValidationProfile,
    [property: JsonPropertyName("workItemRequired")] bool WorkItemRequired,
    [property: JsonPropertyName("traceRequired")] bool TraceRequired,
    [property: JsonPropertyName("description")] string Description);

#pragma warning restore MA0048
