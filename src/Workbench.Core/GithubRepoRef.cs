namespace Workbench
{
    public sealed record GithubRepoRef(string Host, string Owner, string Repo)
    {
        public string Slug => $"{Owner}/{Repo}";
        public string Display => string.Equals(Host, "github.com", StringComparison.OrdinalIgnoreCase)
            ? this.Slug
            : $"{Host}/{this.Slug}";
    }
}
