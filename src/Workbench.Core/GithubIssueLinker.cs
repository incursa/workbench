using System.Text.RegularExpressions;

namespace Workbench.Core;

/// <summary>
/// Encodes and decodes Workbench-specific GitHub issue metadata used for stable sync.
/// </summary>
public static partial class GithubIssueLinker
{
    [GeneratedRegex(
        @"<!--\s*workbench:item\s+(?<itemId>[A-Za-z]+-\d+)\s*-->",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking)]
    private static partial Regex ItemMarkerRegex();

    public static string AppendWorkbenchItemMarker(string body, string itemId)
    {
        if (TryExtractWorkbenchItemId(body, out var existingItemId)
            && string.Equals(existingItemId, itemId, StringComparison.OrdinalIgnoreCase))
        {
            return body.TrimEnd();
        }

        var marker = $"<!-- workbench:item {itemId} -->";
        if (string.IsNullOrWhiteSpace(body))
        {
            return marker;
        }

        return $"{body.TrimEnd()}\n\n{marker}";
    }

    public static bool TryBuildMarkerBackfillBody(string? body, string itemId, out string updatedBody)
    {
        updatedBody = AppendWorkbenchItemMarker(body ?? string.Empty, itemId);
        return !StringsEqual(body, updatedBody);
    }

    public static bool TryExtractWorkbenchItemId(string? body, out string itemId)
    {
        itemId = string.Empty;
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var match = ItemMarkerRegex().Match(body);
        if (!match.Success)
        {
            return false;
        }

        itemId = match.Groups["itemId"].Value.Trim();
        return itemId.Length > 0;
    }

    public static bool TryMatchIssueToItem(
        GithubIssue issue,
        IReadOnlyDictionary<string, WorkItem> itemsById,
        out WorkItem? item)
    {
        item = null;
        if (!TryExtractWorkbenchItemId(issue.Body, out var itemId))
        {
            return false;
        }

        return itemsById.TryGetValue(itemId, out item);
    }

    private static bool StringsEqual(string? left, string? right)
    {
        return string.Equals(
            left?.Replace("\r\n", "\n"),
            right?.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }
}
