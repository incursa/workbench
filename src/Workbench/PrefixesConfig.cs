namespace Workbench
{
    public sealed record PrefixesConfig
    {
        public string Bug { get; init; } = "BUG";
        public string Task { get; init; } = "TASK";
        public string Spike { get; init; } = "SPIKE";
    }
}