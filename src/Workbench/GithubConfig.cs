namespace Workbench
{
    public sealed record GithubConfig
    {
        public string Provider { get; init; } = "octokit";
        public bool DefaultDraft { get; init; } = false;
        public string Host { get; init; } = "github.com";
        public string? Owner { get; init; }
        public string? Repository { get; init; }
    }
}
