using System.Text.RegularExpressions;

namespace Workbench.Core;

public static class WorkItemHierarchyService
{
    public const int CurrentSchemaVersion = 1;
    private const string WorkItemIdPattern = "^WI-[A-Z][A-Z0-9]*(?:-[A-Z][A-Z0-9]*)*-\\d{4,}$";

    public static WorkItemHierarchyData Show(string repoRoot, WorkbenchConfig config, string workItemId)
    {
        var item = Load(repoRoot, config, workItemId);
        var children = WorkItemService.ListItems(repoRoot, config, includeDone: true).Items
            .Where(candidate => string.Equals(candidate.ParentRef, item.Id, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new WorkItemHierarchyData(
            CurrentSchemaVersion,
            item.Id,
            item.ParentRef,
            item.InitiativeRefs,
            children);
    }

    public static IList<string> ValidateRepository(string repoRoot, WorkbenchConfig config)
    {
        var diagnostics = new List<string>();
        var items = WorkItemService.ListItems(repoRoot, config, includeDone: true).Items;
        var itemsById = items.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            ValidateAuthoredReference(item, item.ParentRef, "parent_ref", itemsById, diagnostics);
            foreach (var reference in item.InitiativeRefs)
            {
                ValidateAuthoredReference(item, reference, "initiative_refs", itemsById, diagnostics);
            }
            if (item.InitiativeRefs.Distinct(StringComparer.OrdinalIgnoreCase).Count() != item.InitiativeRefs.Count)
            {
                diagnostics.Add($"{item.Path}: initiative_refs must be duplicate-free.");
            }
            if (CreatesCycle(item.Id, item.ParentRef, itemsById))
            {
                diagnostics.Add($"{item.Path}: parent_ref creates a local hierarchy cycle.");
            }
        }
        return diagnostics;
    }

    public static WorkItemHierarchyData Set(
        string repoRoot,
        WorkbenchConfig config,
        string workItemId,
        bool updateParent,
        string? parentRef,
        bool updateInitiatives,
        IReadOnlyList<string> initiativeRefs)
    {
        var item = Load(repoRoot, config, workItemId);
        var normalizedParent = item.ParentRef;
        if (updateParent)
        {
            normalizedParent = string.IsNullOrWhiteSpace(parentRef)
                ? null
                : NormalizeReference(parentRef);
        }
        var normalizedInitiatives = updateInitiatives
            ? NormalizeInitiatives(initiativeRefs)
            : item.InitiativeRefs.ToList();

        ValidateLocalReference(repoRoot, config, item.Id, normalizedParent, "parent");
        foreach (var reference in normalizedInitiatives)
        {
            ValidateLocalReference(repoRoot, config, item.Id, reference, "initiative");
        }
        ValidateParentCycle(repoRoot, config, item.Id, normalizedParent);

        var content = File.ReadAllText(item.Path);
        if (!FrontMatter.TryParse(content, out var frontMatter, out var error))
        {
            throw new InvalidOperationException($"Front matter error: {error}");
        }

        if (updateParent)
        {
            SetOptionalValue(frontMatter!.Data, "parent_ref", normalizedParent);
        }
        if (updateInitiatives)
        {
            SetOptionalList(frontMatter!.Data, "initiative_refs", normalizedInitiatives);
        }

        var updatedContent = frontMatter!.Serialize();
        if (!string.Equals(content, updatedContent, StringComparison.Ordinal))
        {
            File.WriteAllText(item.Path, updatedContent);
        }
        return Show(repoRoot, config, item.Id);
    }

    public static string NormalizeReference(string reference)
    {
        var trimmed = reference.Trim();
        if (IsLocalReference(trimmed))
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "workbench", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !uri.IsDefaultPort)
        {
            throw new InvalidOperationException($"Invalid work-item reference '{reference}'. Expected WI-... or workbench://host/owner/repository/WI-....");
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();
        if (segments.Length != 3 ||
            segments.Take(2).Any(segment => string.IsNullOrWhiteSpace(segment) || segment.Contains('/', StringComparison.Ordinal)) ||
            !IsLocalReference(segments[2]))
        {
            throw new InvalidOperationException($"Invalid cross-repository work-item reference '{reference}'.");
        }

        return $"workbench://{uri.IdnHost.ToLowerInvariant()}/{segments[0]}/{segments[1]}/{segments[2]}";
    }

    private static List<string> NormalizeInitiatives(IReadOnlyList<string> references)
    {
        var normalized = references.Select(NormalizeReference).ToList();
        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Count)
        {
            throw new InvalidOperationException("Initiative references must be duplicate-free.");
        }
        return normalized;
    }

    private static void ValidateAuthoredReference(
        WorkItem item,
        string? reference,
        string field,
        IReadOnlyDictionary<string, WorkItem> itemsById,
        ICollection<string> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return;
        }

        try
        {
            var normalized = NormalizeReference(reference);
            if (!IsLocalReference(normalized))
            {
                return;
            }
            if (string.Equals(normalized, item.Id, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"{item.Path}: {field} cannot reference the work item itself.");
            }
            else if (!itemsById.ContainsKey(normalized))
            {
                diagnostics.Add($"{item.Path}: {field} references missing local work item '{normalized}'.");
            }
        }
        catch (InvalidOperationException ex)
        {
            diagnostics.Add($"{item.Path}: {field} contains {ex.Message}");
        }
    }

    private static bool CreatesCycle(
        string workItemId,
        string? parentRef,
        IReadOnlyDictionary<string, WorkItem> itemsById)
    {
        var cursor = parentRef;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { workItemId };
        while (cursor is not null && IsLocalReference(cursor) && itemsById.TryGetValue(cursor, out var item))
        {
            if (!visited.Add(cursor))
            {
                return true;
            }
            cursor = item.ParentRef;
        }
        return false;
    }

    private static void ValidateLocalReference(
        string repoRoot,
        WorkbenchConfig config,
        string workItemId,
        string? reference,
        string relationship)
    {
        if (reference is null || !IsLocalReference(reference))
        {
            return;
        }
        if (string.Equals(reference, workItemId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Work item '{workItemId}' cannot reference itself as {relationship}.");
        }

        _ = WorkItemService.GetItemPathById(repoRoot, config, reference);
    }

    private static void ValidateParentCycle(
        string repoRoot,
        WorkbenchConfig config,
        string workItemId,
        string? parentRef)
    {
        var cursor = parentRef;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { workItemId };
        while (cursor is not null && IsLocalReference(cursor))
        {
            if (!visited.Add(cursor))
            {
                throw new InvalidOperationException($"Parent reference '{parentRef}' creates a local hierarchy cycle.");
            }
            cursor = Load(repoRoot, config, cursor).ParentRef;
        }
    }

    private static WorkItem Load(string repoRoot, WorkbenchConfig config, string workItemId)
    {
        var path = WorkItemService.GetItemPathById(repoRoot, config, workItemId);
        return WorkItemService.LoadItem(path)
            ?? throw new InvalidOperationException($"Work item '{workItemId}' could not be loaded.");
    }

    private static bool IsLocalReference(string value)
    {
        return Regex.IsMatch(value, WorkItemIdPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private static void SetOptionalValue(IDictionary<string, object?> data, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = data.Remove(key);
        }
        else
        {
            data[key] = value;
        }
    }

    private static void SetOptionalList(IDictionary<string, object?> data, string key, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            _ = data.Remove(key);
        }
        else
        {
            data[key] = values.Cast<object?>().ToList();
        }
    }
}
