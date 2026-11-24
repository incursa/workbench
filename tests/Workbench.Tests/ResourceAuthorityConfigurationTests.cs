using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class ResourceAuthorityConfigurationTests
{
    [TestMethod]
    public void Validate_AcceptsOneAuthorityWithOptionalNonAuthoritativeRoles()
    {
        var configuration = CreateValidConfiguration();

        var diagnostics = ResourceAuthorityConfigurationValidator.Validate(configuration);
        var binding = ResourceAuthorityConfigurationValidator.Resolve(configuration, "specification", "set-1");

        Assert.IsEmpty(diagnostics);
        Assert.AreEqual("git", binding.AuthorityProvider);
        Assert.AreEqual(
            ResourceProviderRole.Authority,
            ResourceAuthorityConfigurationValidator.ResolveRole(binding, "git"));
        Assert.AreEqual(
            ResourceProviderRole.ReadOnlyEnricher,
            ResourceAuthorityConfigurationValidator.ResolveRole(binding, "search"));
        Assert.AreEqual(
            ResourceProviderRole.PublicationTarget,
            ResourceAuthorityConfigurationValidator.ResolveRole(binding, "publisher"));
    }

    [TestMethod]
    public void Validate_FailsClosedForDuplicateAuthorityDomainsAndRoleOverlap()
    {
        var configuration = CreateValidConfiguration() with
        {
            ResourceSets =
            [
                CreateValidConfiguration().ResourceSets[0] with
                {
                    ReadOnlyEnrichers = ["git", "missing"],
                    ConflictPolicy = "latest-wins",
                    OfflinePolicy = "automatic-failover"
                },
                CreateValidConfiguration().ResourceSets[0]
            ]
        };

        var diagnostics = ResourceAuthorityConfigurationValidator.Validate(configuration);
        var codes = diagnostics.Select(diagnostic => diagnostic.Code).ToList();

        CollectionAssert.Contains(codes, "provider.authority.scope.duplicate");
        CollectionAssert.Contains(codes, "provider.authority.role.overlap");
        CollectionAssert.Contains(codes, "provider.authority.provider.unknown");
        CollectionAssert.Contains(codes, "provider.authority.conflict-policy.unsupported");
        CollectionAssert.Contains(codes, "provider.authority.offline-policy.unsupported");
    }

    [TestMethod]
    public void WorkbenchConfig_LoadsLegacyConfigWithoutProviderSection()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".workbench"));
        File.WriteAllText(
            WorkbenchConfig.GetConfigPath(temp.Path),
            """
            {
              "Paths": {},
              "Ids": {},
              "Git": {},
              "Github": {},
              "Validation": {},
              "Tui": {}
            }
            """);

        var config = WorkbenchConfig.Load(temp.Path, out var error);

        Assert.IsNull(error);
        Assert.IsNotNull(config.ResourceProviders);
        Assert.IsEmpty(config.ResourceProviders.Providers);
        Assert.IsEmpty(config.ResourceProviders.ResourceSets);
    }

    [TestMethod]
    public void Validate_ReportsNullCollectionsFromMalformedConfiguration()
    {
        var configuration = new ResourceProvidersConfig
        {
            Providers = null!,
            ResourceSets = null!
        };

        var diagnostics = ResourceAuthorityConfigurationValidator.Validate(configuration);
        var codes = diagnostics.Select(diagnostic => diagnostic.Code).ToList();

        CollectionAssert.Contains(codes, "provider.config.providers.required");
        CollectionAssert.Contains(codes, "provider.config.resource-sets.required");
    }

    [TestMethod]
    public void Validate_RejectsTimeoutsOutsidePublishedSchemaRange()
    {
        ResourceProvidersConfig configuration = CreateValidConfiguration() with
        {
            DefaultTimeoutSeconds = 3601,
            Providers =
            [
                CreateValidConfiguration().Providers[0] with { TimeoutSeconds = 3601 },
                .. CreateValidConfiguration().Providers.Skip(1)
            ]
        };

        string[] codes = ResourceAuthorityConfigurationValidator.Validate(configuration)
            .Select(static diagnostic => diagnostic.Code)
            .ToArray();

        Assert.HasCount(2, codes.Where(static code => string.Equals(code, "provider.config.timeout.invalid", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ProviderConfiguration_ValidatesAgainstStandaloneAndRootSchemas()
    {
        string repoRoot = FindRepoRoot();
        WorkbenchConfig config = WorkbenchConfig.Default with
        {
            ResourceProviders = CreateValidConfiguration()
        };
        string configJson = JsonSerializer.Serialize(config, WorkbenchJsonContext.Default.WorkbenchConfig);
        using JsonDocument document = JsonDocument.Parse(configJson);
        string providersJson = document.RootElement.GetProperty("ResourceProviders").GetRawText();

        IReadOnlyList<string> rootErrors = (IReadOnlyList<string>)SchemaValidationService.ValidateJsonContent(
            repoRoot,
            "schemas/workbench-config.schema.json",
            "workbench-config",
            configJson);
        IReadOnlyList<string> providerErrors = (IReadOnlyList<string>)SchemaValidationService.ValidateJsonContent(
            repoRoot,
            "specs/schemas/resource-authority-config.schema.json",
            "resource-providers",
            providersJson);

        Assert.IsEmpty(rootErrors, string.Join(Environment.NewLine, rootErrors));
        Assert.IsEmpty(providerErrors, string.Join(Environment.NewLine, providerErrors));
    }

    private static ResourceProvidersConfig CreateValidConfiguration()
    {
        return new ResourceProvidersConfig
        {
            Providers =
            [
                new ResourceProviderProcessConfig { Id = "git", Executable = "workbench-git-provider" },
                new ResourceProviderProcessConfig { Id = "search", Executable = "workbench-search-provider" },
                new ResourceProviderProcessConfig { Id = "publisher", Executable = "workbench-publisher" }
            ],
            ResourceSets =
            [
                new ResourceAuthorityBindingConfig
                {
                    ResourceType = "specification",
                    ResourceSet = "set-1",
                    AuthorityProvider = "git",
                    ReadOnlyEnrichers = ["search"],
                    PublicationTargets = ["publisher"]
                }
            ]
        };
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Workbench.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Workbench.slnx.");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            this.Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "workbench-resource-provider-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
    }
}
