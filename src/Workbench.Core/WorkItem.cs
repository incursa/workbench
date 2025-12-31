namespace Workbench
{
    public sealed record WorkItem(
        string Id,
        string Type,
        string Status,
        string Title,
        string? Priority,
        string? Owner,
        string Created,
        string? Updated,
        IList<string> Tags,
        RelatedLinks Related,
        string Slug,
        string Path,
        string Body);
}
