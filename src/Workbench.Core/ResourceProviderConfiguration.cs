using System.Text.Json.Serialization;

#pragma warning disable MA0048 // Provider configuration DTOs form one versioned configuration contract.

namespace Workbench.Core;

/// <summary>
/// Configures out-of-process resource providers and the authority assignment for each resource set.
/// </summary>
public sealed record ResourceProvidersConfig
{
    /// <summary>Provider process definitions keyed by their stable <see cref="ResourceProviderProcessConfig.Id"/>.</summary>
    public IReadOnlyList<ResourceProviderProcessConfig> Providers { get; init; } = [];

    /// <summary>Authority assignments. A resource type/set pair may appear at most once.</summary>
    public IReadOnlyList<ResourceAuthorityBindingConfig> ResourceSets { get; init; } = [];

    /// <summary>Default request timeout when a provider does not override it.</summary>
    public int DefaultTimeoutSeconds { get; init; } = 30;
}

/// <summary>
/// Describes an executable provider endpoint. Workbench starts this process and communicates only through stdin/stdout.
/// </summary>
public sealed record ResourceProviderProcessConfig
{
    /// <summary>Stable provider identifier used by authority bindings and provenance records.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Executable to launch directly. Workbench never loads a provider assembly into its own process.</summary>
    public string Executable { get; init; } = string.Empty;

    /// <summary>Arguments passed without shell expansion.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Optional repo-relative or absolute working directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Per-request timeout. A null value uses <see cref="ResourceProvidersConfig.DefaultTimeoutSeconds"/>.</summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>Disabled providers remain configured but cannot be assigned a live role.</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Assigns exactly one live write authority to a resource type/set pair and optional non-authoritative roles.
/// </summary>
public sealed record ResourceAuthorityBindingConfig
{
    /// <summary>Provider-neutral resource type, such as <c>work_item</c> or <c>specification</c>.</summary>
    public string ResourceType { get; init; } = string.Empty;

    /// <summary>Stable resource-set identifier. Wildcard or overlapping set declarations are not supported initially.</summary>
    public string ResourceSet { get; init; } = string.Empty;

    /// <summary>The sole provider allowed to write normative content for this resource type/set pair.</summary>
    public string AuthorityProvider { get; init; } = string.Empty;

    /// <summary>Providers that may read and return derived enrichment but may not write normative content.</summary>
    public IReadOnlyList<string> ReadOnlyEnrichers { get; init; } = [];

    /// <summary>Providers that may receive explicit publication operations but never become content authority.</summary>
    public IReadOnlyList<string> PublicationTargets { get; init; } = [];

    /// <summary>Conflict behavior is intentionally fixed to <c>fail</c> for the initial contract.</summary>
    public string ConflictPolicy { get; init; } = ResourceAuthorityPolicies.Fail;

    /// <summary>Offline behavior is intentionally fixed to immutable store-and-forward packages.</summary>
    public string OfflinePolicy { get; init; } = ResourceAuthorityPolicies.StoreAndForward;
}

/// <summary>Fixed policy names for the initial authority foundation.</summary>
public static class ResourceAuthorityPolicies
{
    public const string Fail = "fail";
    public const string StoreAndForward = "store-and-forward";
}

/// <summary>Role resolved for a provider within one resource type/set authority domain.</summary>
public enum ResourceProviderRole
{
    [JsonStringEnumMemberName("authority")]
    Authority,
    [JsonStringEnumMemberName("readOnlyEnricher")]
    ReadOnlyEnricher,
    [JsonStringEnumMemberName("publicationTarget")]
    PublicationTarget
}

/// <summary>One configuration diagnostic produced before any provider process is started.</summary>
public sealed record ResourceProviderConfigurationDiagnostic(
    string Code,
    string Message,
    string? Target = null);

#pragma warning restore MA0048
