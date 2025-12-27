namespace Workbench
{
    public sealed class ValidationResult
    {
        public IList<string> Errors { get; } = new List<string>();
        public IList<string> Warnings { get; } = new List<string>();
        public int WorkItemCount { get; set; }
        public int MarkdownFileCount { get; set; }
    }
}
