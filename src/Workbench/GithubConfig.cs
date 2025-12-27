namespace Workbench
{
    public sealed record GithubConfig
    {
        public string Provider { get; init; } = "gh";
        public bool DefaultDraft { get; init; } = false;
    }
}