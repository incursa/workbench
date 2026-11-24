namespace Workbench.Core;

/// <summary>
/// Validates and resolves declared resource authority without contacting providers.
/// </summary>
public static class ResourceAuthorityConfigurationValidator
{
    /// <summary>Returns all configuration errors. An empty result means the authority graph is usable.</summary>
    public static IReadOnlyList<ResourceProviderConfigurationDiagnostic> Validate(ResourceProvidersConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var diagnostics = new List<ResourceProviderConfigurationDiagnostic>();
        if (configuration.DefaultTimeoutSeconds is <= 0 or > 3600)
        {
            diagnostics.Add(new(
                "provider.config.timeout.invalid",
                "DefaultTimeoutSeconds must be between 1 and 3600.",
                "ResourceProviders.DefaultTimeoutSeconds"));
        }

        var providerDefinitions = configuration.Providers;
        if (providerDefinitions is null)
        {
            diagnostics.Add(new(
                "provider.config.providers.required",
                "Providers must be an array.",
                "ResourceProviders.Providers"));
            providerDefinitions = [];
        }

        var providers = new Dictionary<string, ResourceProviderProcessConfig>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < providerDefinitions.Count; index++)
        {
            var provider = providerDefinitions[index];
            var target = $"ResourceProviders.Providers[{index}]";
            if (provider is null)
            {
                diagnostics.Add(new(
                    "provider.config.provider.required",
                    "Provider entries must be objects.",
                    target));
                continue;
            }

            if (string.IsNullOrWhiteSpace(provider.Id))
            {
                diagnostics.Add(new("provider.config.id.required", "Provider Id is required.", target));
                continue;
            }

            if (!providers.TryAdd(provider.Id, provider))
            {
                diagnostics.Add(new(
                    "provider.config.id.duplicate",
                    $"Provider Id '{provider.Id}' is declared more than once.",
                    target));
            }

            if (string.IsNullOrWhiteSpace(provider.Executable))
            {
                diagnostics.Add(new(
                    "provider.config.executable.required",
                    $"Provider '{provider.Id}' must declare an executable.",
                    target));
            }

            if (provider.TimeoutSeconds is <= 0 or > 3600)
            {
                diagnostics.Add(new(
                    "provider.config.timeout.invalid",
                    $"Provider '{provider.Id}' TimeoutSeconds must be between 1 and 3600 when specified.",
                    target));
            }

            if (provider.Arguments is null)
            {
                diagnostics.Add(new(
                    "provider.config.arguments.required",
                    $"Provider '{provider.Id}' Arguments must be an array.",
                    target));
            }
        }

        var resourceSets = configuration.ResourceSets;
        if (resourceSets is null)
        {
            diagnostics.Add(new(
                "provider.config.resource-sets.required",
                "ResourceSets must be an array.",
                "ResourceProviders.ResourceSets"));
            resourceSets = [];
        }

        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < resourceSets.Count; index++)
        {
            var binding = resourceSets[index];
            var target = $"ResourceProviders.ResourceSets[{index}]";
            if (binding is null)
            {
                diagnostics.Add(new(
                    "provider.authority.binding.required",
                    "Resource set entries must be objects.",
                    target));
                continue;
            }

            ValidateBinding(binding, target, providers, scopes, diagnostics);
        }

        return diagnostics;
    }

    /// <summary>Returns the one binding for an exact resource type/set pair.</summary>
    public static ResourceAuthorityBindingConfig Resolve(
        ResourceProvidersConfig configuration,
        string resourceType,
        string resourceSet)
    {
        var diagnostics = Validate(configuration);
        if (diagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                "Resource provider configuration is invalid: "
                + string.Join("; ", diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return configuration.ResourceSets.SingleOrDefault(binding =>
            string.Equals(binding.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(binding.ResourceSet, resourceSet, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No authority is declared for resource type '{resourceType}' and set '{resourceSet}'.");
    }

    /// <summary>Resolves a configured provider's role within an exact authority domain.</summary>
    public static ResourceProviderRole ResolveRole(ResourceAuthorityBindingConfig binding, string providerId)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (string.Equals(binding.AuthorityProvider, providerId, StringComparison.OrdinalIgnoreCase))
        {
            return ResourceProviderRole.Authority;
        }

        if (binding.ReadOnlyEnrichers?.Contains(providerId, StringComparer.OrdinalIgnoreCase) == true)
        {
            return ResourceProviderRole.ReadOnlyEnricher;
        }

        if (binding.PublicationTargets?.Contains(providerId, StringComparer.OrdinalIgnoreCase) == true)
        {
            return ResourceProviderRole.PublicationTarget;
        }

        throw new InvalidOperationException(
            $"Provider '{providerId}' has no role for resource type '{binding.ResourceType}' and set '{binding.ResourceSet}'.");
    }

    private static void ValidateBinding(
        ResourceAuthorityBindingConfig binding,
        string target,
        IReadOnlyDictionary<string, ResourceProviderProcessConfig> providers,
        ISet<string> scopes,
        ICollection<ResourceProviderConfigurationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(binding.ResourceType) || string.IsNullOrWhiteSpace(binding.ResourceSet))
        {
            diagnostics.Add(new(
                "provider.authority.scope.required",
                "ResourceType and ResourceSet are required for every authority binding.",
                target));
            return;
        }

        var scope = $"{binding.ResourceType}\n{binding.ResourceSet}";
        if (!scopes.Add(scope))
        {
            diagnostics.Add(new(
                "provider.authority.scope.duplicate",
                $"Resource type '{binding.ResourceType}' and set '{binding.ResourceSet}' have more than one authority declaration.",
                target));
        }

        if (string.IsNullOrWhiteSpace(binding.AuthorityProvider))
        {
            diagnostics.Add(new(
                "provider.authority.required",
                $"Resource type '{binding.ResourceType}' and set '{binding.ResourceSet}' must declare exactly one authority provider.",
                target));
        }
        else
        {
            ValidateProviderReference(binding.AuthorityProvider, "authority", target, providers, diagnostics);
        }

        var readOnlyEnrichers = binding.ReadOnlyEnrichers;
        if (readOnlyEnrichers is null)
        {
            diagnostics.Add(new(
                "provider.authority.read-only-enrichers.required",
                "ReadOnlyEnrichers must be an array.",
                target));
            readOnlyEnrichers = [];
        }

        var publicationTargets = binding.PublicationTargets;
        if (publicationTargets is null)
        {
            diagnostics.Add(new(
                "provider.authority.publication-targets.required",
                "PublicationTargets must be an array.",
                target));
            publicationTargets = [];
        }

        ValidateRoleReferences(readOnlyEnrichers, "read-only enricher", target, providers, diagnostics);
        ValidateRoleReferences(publicationTargets, "publication target", target, providers, diagnostics);

        var allRoles = new[] { binding.AuthorityProvider }
            .Concat(readOnlyEnrichers)
            .Concat(publicationTargets)
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .ToList();
        var duplicateRole = allRoles
            .GroupBy(provider => provider, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateRole is not null)
        {
            diagnostics.Add(new(
                "provider.authority.role.overlap",
                $"Provider '{duplicateRole.Key}' cannot hold more than one role in the same resource authority domain.",
                target));
        }

        if (!string.Equals(binding.ConflictPolicy, ResourceAuthorityPolicies.Fail, StringComparison.Ordinal))
        {
            diagnostics.Add(new(
                "provider.authority.conflict-policy.unsupported",
                "ConflictPolicy must be 'fail'; automatic winner selection is prohibited.",
                target));
        }

        if (!string.Equals(binding.OfflinePolicy, ResourceAuthorityPolicies.StoreAndForward, StringComparison.Ordinal))
        {
            diagnostics.Add(new(
                "provider.authority.offline-policy.unsupported",
                "OfflinePolicy must be 'store-and-forward'; peer unavailability cannot change authority.",
                target));
        }
    }

    private static void ValidateRoleReferences(
        IReadOnlyList<string> providerIds,
        string role,
        string target,
        IReadOnlyDictionary<string, ResourceProviderProcessConfig> providers,
        ICollection<ResourceProviderConfigurationDiagnostic> diagnostics)
    {
        foreach (var providerId in providerIds)
        {
            ValidateProviderReference(providerId, role, target, providers, diagnostics);
        }
    }

    private static void ValidateProviderReference(
        string providerId,
        string role,
        string target,
        IReadOnlyDictionary<string, ResourceProviderProcessConfig> providers,
        ICollection<ResourceProviderConfigurationDiagnostic> diagnostics)
    {
        if (!providers.TryGetValue(providerId, out var provider))
        {
            diagnostics.Add(new(
                "provider.authority.provider.unknown",
                $"The {role} provider '{providerId}' is not declared.",
                target));
            return;
        }

        if (!provider.Enabled)
        {
            diagnostics.Add(new(
                "provider.authority.provider.disabled",
                $"The {role} provider '{providerId}' is disabled.",
                target));
        }
    }
}
