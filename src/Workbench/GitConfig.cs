namespace Workbench
{
    public sealed record GitConfig
    {
        public string BranchPattern { get; init; } = "work/{id}-{slug}";
        public string CommitMessagePattern { get; init; } = "Promote {id}: {title}";
        public string DefaultBaseBranch { get; init; } = "main";
        public bool RequireCleanWorkingTree { get; init; } = true;
    }
}