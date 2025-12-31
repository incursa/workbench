namespace Workbench
{
    public sealed record IdsConfig
    {
        public int Width { get; init; } = 4;
        public PrefixesConfig Prefixes { get; init; } = new();
    }
}
