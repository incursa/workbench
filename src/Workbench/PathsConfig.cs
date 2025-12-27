namespace Workbench
{
    public sealed record PathsConfig
    {
        public string DocsRoot { get; init; } = "docs";
        public string WorkRoot { get; init; } = "work";
        public string ItemsDir { get; init; } = "work/items";
        public string DoneDir { get; init; } = "work/done";
        public string TemplatesDir { get; init; } = "work/templates";
        public string WorkboardFile { get; init; } = "work/WORKBOARD.md";
    }
}