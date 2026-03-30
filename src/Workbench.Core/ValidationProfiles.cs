namespace Workbench.Core;

/// <summary>
/// Canonical validation profiles and helpers for profile gating.
/// </summary>
public static class ValidationProfiles
{
    public const string Core = "core";
    public const string Traceable = "traceable";
    public const string Auditable = "auditable";
    public const string RepoState = "repo-state";

    private static readonly IReadOnlyDictionary<string, int> profileLevels =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Core] = 0,
            [Traceable] = 1,
            [Auditable] = 2
        };

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = Core;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim().ToLowerInvariant();
        if (profileLevels.ContainsKey(candidate))
        {
            normalized = candidate;
            return true;
        }

        return false;
    }

    public static string NormalizeOrDefault(string? value, string defaultProfile = Core)
    {
        if (TryNormalize(value, out var normalized))
        {
            return normalized;
        }

        return defaultProfile;
    }

    public static bool IsEnabledFor(string selectedProfile, string profileGroup)
    {
        if (string.Equals(profileGroup, RepoState, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!profileLevels.TryGetValue(selectedProfile, out var selectedLevel))
        {
            selectedLevel = 0;
        }

        if (!profileLevels.TryGetValue(profileGroup, out var requiredLevel))
        {
            return false;
        }

        return selectedLevel >= requiredLevel;
    }

    public static string Resolve(string? requestedProfile, string? configuredProfile, out string? error)
    {
        if (!string.IsNullOrWhiteSpace(requestedProfile))
        {
            if (TryNormalize(requestedProfile, out var normalizedRequested))
            {
                error = null;
                return normalizedRequested;
            }

            error = $"Unknown validation profile '{requestedProfile}'. Expected core, traceable, or auditable.";
            return Core;
        }

        if (!string.IsNullOrWhiteSpace(configuredProfile))
        {
            if (TryNormalize(configuredProfile, out var normalizedConfigured))
            {
                error = null;
                return normalizedConfigured;
            }

            error = $"Unknown configured validation profile '{configuredProfile}'. Expected core, traceable, or auditable.";
            return Core;
        }

        error = null;
        return Core;
    }
}
