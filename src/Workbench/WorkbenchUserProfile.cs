namespace Workbench;

public sealed record WorkbenchUserProfile
{
    public string? DisplayName { get; init; }

    public string? Handle { get; init; }

    public string? Email { get; init; }

    public string? DefaultOwner { get; init; }

    public string EffectiveAuthor =>
        FirstNonEmpty(DefaultOwner, DisplayName, Handle) ?? string.Empty;

    public string Summary =>
        string.Join(
            " / ",
            new[] { DisplayName, Handle, Email }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
