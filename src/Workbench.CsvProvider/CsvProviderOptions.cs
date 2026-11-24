using Workbench.Core;

#pragma warning disable MA0048 // CSV provider configuration constants share one small contract file.

namespace Workbench.CsvProvider;

public sealed record CsvProviderOptions
{
    public const string DefaultProviderId = "workbench-csv";
    public const string DefaultProjectionProviderId = "workbench-csv-projection";
    public const string CurrentProviderVersion = "1.0.0";

    public required string RootDirectory { get; init; }

    public required string ResourceSet { get; init; }

    public string ProviderId { get; init; } = DefaultProviderId;

    public string ProviderVersion { get; init; } = CurrentProviderVersion;

    public ResourceProviderRole Role { get; init; } = ResourceProviderRole.Authority;

    public string AuthorityProviderId { get; init; } = DefaultProviderId;

    public required string AuthorityEpoch { get; init; }

    public int MaxBatchSize { get; init; } = 500;

    public int PlanLifetimeMinutes { get; init; } = 30;

    public string GetCsvPath(string resourceType)
    {
        string fileName = resourceType switch
        {
            CsvResourceTypes.WorkItem => "work-items.v1.csv",
            CsvResourceTypes.Specification => "specifications.v1.csv",
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unsupported CSV resource type.")
        };
        return Path.Combine(Path.GetFullPath(this.RootDirectory), fileName);
    }

    internal string StateDirectory => Path.Combine(Path.GetFullPath(this.RootDirectory), ".workbench-csv-provider");

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.RootDirectory)
            || string.IsNullOrWhiteSpace(this.ResourceSet)
            || string.IsNullOrWhiteSpace(this.ProviderId)
            || string.IsNullOrWhiteSpace(this.ProviderVersion)
            || string.IsNullOrWhiteSpace(this.AuthorityProviderId)
            || string.IsNullOrWhiteSpace(this.AuthorityEpoch))
        {
            throw new InvalidOperationException("CSV provider paths, identity, resource set, and authority epoch are required.");
        }

        if (this.MaxBatchSize <= 0)
        {
            throw new InvalidOperationException("CSV provider maximum batch size must be positive.");
        }

        if (this.PlanLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException("CSV provider plan lifetime must be positive.");
        }

        if (this.Role == ResourceProviderRole.PublicationTarget)
        {
            throw new InvalidOperationException("The CSV provider supports authority and editable-projection roles, not publication.");
        }

        if (this.Role == ResourceProviderRole.Authority
            && !string.Equals(this.AuthorityProviderId, this.ProviderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An authoritative CSV provider must identify itself as the declared authority provider.");
        }

        if (this.Role == ResourceProviderRole.ReadOnlyEnricher
            && string.Equals(this.AuthorityProviderId, this.ProviderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An editable CSV projection must name its external authority provider explicitly.");
        }
    }
}

public static class CsvResourceTypes
{
    public const string WorkItem = "work_item";
    public const string Specification = "specification";

    public static IReadOnlyList<string> All { get; } = [WorkItem, Specification];
}

public static class CsvSchemaVersions
{
    public const string WorkItemV1 = "workbench.csv.work-item.v1";
    public const string SpecificationV1 = "workbench.csv.specification.v1";

    public static string ForResourceType(string resourceType)
    {
        return resourceType switch
        {
            CsvResourceTypes.WorkItem => WorkItemV1,
            CsvResourceTypes.Specification => SpecificationV1,
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unsupported CSV resource type.")
        };
    }
}

#pragma warning restore MA0048
