namespace Workbench
{
    public sealed record PathsConfig
    {
        public string DocsRoot { get; init; } = "docs";
        public string WorkRoot { get; init; } = "docs/70-work";
        public string ItemsDir { get; init; } = "docs/70-work/items";
        public string DoneDir { get; init; } = "docs/70-work/done";
        public string TemplatesDir { get; init; } = "docs/70-work/templates";
        public string WorkboardFile { get; init; } = "docs/70-work/README.md";
    }
}
