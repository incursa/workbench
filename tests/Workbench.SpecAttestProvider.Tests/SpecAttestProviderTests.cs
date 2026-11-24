using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Workbench.Core;
using Workbench.SpecAttestProvider;

namespace Workbench.SpecAttestProvider.Tests;

[TestClass]
public sealed class SpecAttestProviderTests
{
    private static readonly DateTimeOffset now = new(2026, 11, 22, 7, 0, 0, TimeSpan.Zero);
    private const string SetId = "018f5f6e-f5d9-7a55-8f0f-63b518fb19a4";

    [TestMethod]
    public void Options_AcceptLoopbackLocalAndRequireHttpsForHosted()
    {
        SpecAttestProviderOptions local = CreateOptions(new Uri("http://127.0.0.1:5080"), SpecAttestEndpointMode.Local);
        local.Validate();

        SpecAttestProviderOptions hostedHttp = CreateOptions(
            new Uri("http://manager.example.test"),
            SpecAttestEndpointMode.Hosted);
        Assert.Throws<InvalidOperationException>(hostedHttp.Validate);

        SpecAttestProviderOptions nonLoopbackLocal = CreateOptions(
            new Uri("https://manager.example.test"),
            SpecAttestEndpointMode.Local);
        Assert.Throws<InvalidOperationException>(nonLoopbackLocal.Validate);

        SpecAttestProviderOptions hostedWithoutToken = CreateOptions(
            new Uri("https://manager.example.test"),
            SpecAttestEndpointMode.Hosted);
        Assert.Throws<InvalidOperationException>(hostedWithoutToken.Validate);
    }

    [TestMethod]
    public void Options_RequireTheCompleteImmutablePublishedEditionTuple()
    {
        CreateOptions().Validate();

        Assert.Throws<ArgumentException>(() => (CreateOptions() with
        {
            StandardPin = CreatePin() with { Revision = "main" }
        }).Validate());
        Assert.Throws<ArgumentException>(() => (CreateOptions() with
        {
            StandardPin = CreatePin() with { PayloadSha256 = new string('0', 64) }
        }).Validate());
        Assert.Throws<ArgumentException>(() => (CreateOptions() with
        {
            StandardPin = CreatePin() with { SignatureReference = "refs/heads/main" }
        }).Validate());
    }

    [TestMethod]
    public async Task ManagerClient_ReadsCanonicalExportAndUsesBearerAuthenticationAsync()
    {
        RecordingHandler handler = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/version"] = CreateVersionJson(),
            ["/api/v1/projects/manager-project-1/canonical-export"] = CreateManagerExportJson()
        });
        using HttpClient httpClient = new(handler);
        SpecAttestProviderOptions options = CreateOptions() with { AccessToken = "test-token" };
        SpecAttestManagerClient client = new(httpClient, options);

        SpecAttestManagerExport result = await client.GetCanonicalExportAsync("manager-project-1");

        Assert.AreEqual("spec-trace-manager.project.v5", result.PackageVersion);
        Assert.AreEqual("manager-project-1", result.ProjectId);
        Assert.AreEqual(7, result.RecordVersion);
        CollectionAssert.Contains(
            handler.RequestUris.Select(static uri => uri.AbsolutePath).ToArray(),
            "/version");
        CollectionAssert.Contains(
            handler.RequestUris.Select(static uri => uri.AbsolutePath).ToArray(),
            "/api/v1/projects/manager-project-1/canonical-export");
        Assert.AreEqual(
            new Uri("http://127.0.0.1:5080/api/v1/projects/manager-project-1/canonical-export"),
            handler.RequestUris[^1]);
        Assert.AreEqual(new AuthenticationHeaderValue("Bearer", "test-token"), handler.Authorization);
    }

    [TestMethod]
    public async Task ManagerClient_ReportsTransportFailureWithoutLeakingExceptionDetailAsync()
    {
        using HttpClient httpClient = new(new ThrowingHandler());
        SpecAttestManagerClient client = new(httpClient, CreateOptions());

        SpecAttestManagerStatus result = await client.GetStatusAsync(deep: false);

        Assert.AreEqual(ProviderHealthState.Unavailable, result.State);
        Assert.HasCount(1, result.Diagnostics);
        Assert.AreEqual("specattest.endpoint.unavailable", result.Diagnostics[0].Code);
        Assert.IsTrue(result.Diagnostics[0].Retryable);
        Assert.DoesNotContain("sensitive transport detail", result.Diagnostics[0].Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ManagerClient_RejectsErrorResponseWithoutLeakingResponseBodyAsync()
    {
        using HttpClient httpClient = new(new ErrorHandler("sensitive response detail"));
        SpecAttestManagerClient client = new(httpClient, CreateOptions());

        ResourceProviderProtocolException exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(
            () => client.GetCanonicalExportAsync("manager-project-1"));

        Assert.AreEqual("specattest.endpoint.request-failed", exception.Error.Code);
        Assert.DoesNotContain("sensitive response detail", exception.ToString(), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Provider_ExportsDeterministicUnattestedInterchangeSnapshotAsync()
    {
        SpecAttestProviderOptions options = CreateOptions();
        FakeManagerClient manager = new(CreateManagerExport());
        SpecAttestResourceProvider provider = new(manager, options, new FixedTimeProvider(now));
        ResourceAddress address = new(SpecAttestInterchangeContract.ResourceType, SetId, SetId);

        ProviderGetResponse first = await provider.GetAsync(new ProviderGetRequest([address]));
        ProviderGetResponse second = await provider.GetAsync(new ProviderGetRequest([address]));
        ProviderCapabilities capabilities = await provider.GetCapabilitiesAsync();

        Assert.HasCount(1, first.Resources);
        Assert.AreEqual(first.Resources[0].PayloadSha256, second.Resources[0].PayloadSha256);
        ResourceCompatibilityPin actualPin = first.Resources[0].CompatibilityPin
            ?? throw new AssertFailedException("The exported resource did not retain its Standard pin.");
        Assert.AreEqual(CreatePin().Revision, actualPin.Revision);
        CollectionAssert.AreEqual(
            CreatePin().CanonicalProfiles!.ToArray(),
            actualPin.CanonicalProfiles!.ToArray());
        Assert.HasCount(1, first.Diagnostics);
        Assert.AreEqual("specattest.validation.not-attested", first.Diagnostics[0].Code);
        CollectionAssert.AreEqual(
            new[] { ResourceProviderRole.Authority },
            capabilities.Roles.ToArray());
        CollectionAssert.Contains(capabilities.Operations.ToArray(), ResourceProviderOperations.PlanChanges);
        Assert.IsTrue(capabilities.SupportsIdempotency);
        Assert.IsTrue(capabilities.SupportsOptimisticConcurrency);

        SpecAttestInterchangePackage? package = first.Resources[0].Content.Deserialize(
            SpecAttestProviderJsonContext.Default.SpecAttestInterchangePackage);
        Assert.IsNotNull(package);
        Assert.AreEqual(SpecAttestInterchangeContract.Format, package.Manifest.Format);
        Assert.AreEqual(SetId, package.Manifest.SetId);
        Assert.AreEqual("manager", package.Manifest.AuthorityMode);
        Assert.AreEqual(SpecAttestInterchangeContract.PayloadMediaType, package.Manifest.Payload.MediaType);
        Assert.IsFalse(package.Manifest.Validation.CanonicalProfilePassed);
        Assert.HasCount(1, package.ExternalReferences);
        Assert.AreEqual("https://example.test/reference/1", package.ExternalReferences[0].GetProperty("uri").GetString());
        Assert.IsTrue(package.Entries.Single(entry => string.Equals(
                entry.StableId,
                "REQ-DEMO-0001",
                StringComparison.Ordinal))
            .CanonicalContent.TryGetProperty("x_extension", out _));
        JsonElement requirement = package.Entries.Single(entry => string.Equals(
                entry.StableId,
                "REQ-DEMO-0001",
                StringComparison.Ordinal))
            .CanonicalContent;
        Assert.AreEqual(7, requirement.GetProperty("revision").GetInt32());
        Assert.AreEqual(
            "REQ-DEMO-LEGACY-0001",
            requirement.GetProperty("trace").GetProperty("supersedes")[0].GetString());

        byte[] firstArchive = SpecAttestInterchange.BuildPayloadArchive(package);
        byte[] secondArchive = SpecAttestInterchange.BuildPayloadArchive(package);
        CollectionAssert.AreEqual(firstArchive, secondArchive);
        using MemoryStream archiveStream = new(firstArchive);
        using ZipArchive archive = new(archiveStream, ZipArchiveMode.Read);
        CollectionAssert.Contains(
            archive.Entries.Select(static entry => entry.FullName).ToArray(),
            "entry-manifest.json");
        CollectionAssert.Contains(
            archive.Entries.Select(static entry => entry.FullName).ToArray(),
            "requirements/REQ-DEMO-0001.json");

        SpecAttestInterchangePackage imported = SpecAttestInterchange.ImportPayloadArchive(
            firstArchive,
            package.Manifest);
        byte[] roundTrippedArchive = SpecAttestInterchange.BuildPayloadArchive(imported);
        CollectionAssert.AreEqual(firstArchive, roundTrippedArchive);
        JsonElement roundTrippedRequirement = imported.Entries.Single(entry => string.Equals(
                entry.StableId,
                "REQ-DEMO-0001",
                StringComparison.Ordinal))
            .CanonicalContent;
        Assert.AreEqual(7, roundTrippedRequirement.GetProperty("revision").GetInt64());
        Assert.AreEqual(
            "REQ-DEMO-LEGACY-0001",
            roundTrippedRequirement.GetProperty("trace").GetProperty("supersedes")[0].GetString());
    }

    [TestMethod]
    public void InterchangeValidation_AcceptsExactCoordinatesAndFailsClosedOnTampering()
    {
        SpecAttestProviderOptions options = CreateOptions();
        SpecAttestInterchangePackage package = SpecAttestInterchange.FromManagerExport(
            CreateManagerExport(),
            options,
            "test-version");
        package = package with
        {
            Manifest = package.Manifest with
            {
                Validation = new SpecAttestInterchangeValidation(
                    CanonicalProfilePassed: true,
                    now,
                    SpecAttestCanonicalJson.Sha256("passed"))
            }
        };

        IReadOnlyList<ProviderDiagnostic> valid = SpecAttestInterchangeValidator.ValidateForAcceptance(
            package,
            options.StandardPin,
            SetId,
            SpecAttestAuthorityModes.Manager);
        Assert.IsEmpty(valid, string.Join(Environment.NewLine, valid.Select(static item => item.Message)));

        SpecAttestInterchangeEntry first = package.Entries[0];
        SpecAttestInterchangePackage tampered = package with
        {
            Manifest = package.Manifest with { AuthorityMode = SpecAttestAuthorityModes.GitWorkbench },
            Entries = [first with { Sha256 = new string('0', 64) }, .. package.Entries.Skip(1)]
        };
        string[] codes = SpecAttestInterchangeValidator.ValidateForAcceptance(
                tampered,
                options.StandardPin,
                SetId,
                SpecAttestAuthorityModes.Manager)
            .Select(static item => item.Code)
            .ToArray();
        CollectionAssert.Contains(codes, "specattest.interchange.authority.conflict");
        CollectionAssert.Contains(codes, "specattest.interchange.entry.digest-mismatch");
        CollectionAssert.Contains(codes, "specattest.interchange.entry-manifest.digest-mismatch");
        CollectionAssert.Contains(codes, "specattest.interchange.payload.digest-mismatch");
    }

    [TestMethod]
    public void InterchangeValidation_RejectsUnsupportedTransitionAndMissingRetiredLedger()
    {
        SpecAttestProviderOptions options = CreateOptions();
        string jsonWithoutLedger = CreateManagerExportJson().Replace(
            "\"retiredRequirementLedger\": { \"retired_requirements\": [] },",
            string.Empty,
            StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(jsonWithoutLedger);
        SpecAttestManagerExport export = new(
            "spec-trace-manager.project.v5",
            "manager-project-1",
            7,
            now,
            document.RootElement.Clone());
        SpecAttestInterchangePackage package = SpecAttestInterchange.FromManagerExport(
            export,
            options,
            "test-version");
        package = package with
        {
            Manifest = package.Manifest with
            {
                Transition = "authority-transfer",
                Validation = new SpecAttestInterchangeValidation(
                    CanonicalProfilePassed: true,
                    now,
                    SpecAttestCanonicalJson.Sha256("passed"))
            }
        };

        string[] codes = SpecAttestInterchangeValidator.ValidateForAcceptance(
                package,
                options.StandardPin,
                SetId,
                SpecAttestAuthorityModes.Manager)
            .Select(static item => item.Code)
            .ToArray();

        CollectionAssert.Contains(codes, "specattest.interchange.transition.unsupported");
        CollectionAssert.Contains(codes, "specattest.interchange.retired-ledger.required");
    }

    [TestMethod]
    public void ManagerExport_RejectsUnsafeStableIdentifierBeforeBuildingArchive()
    {
        SpecAttestProviderOptions options = CreateOptions();
        string unsafeJson = CreateManagerExportJson().Replace(
            "REQ-DEMO-0001",
            "../outside",
            StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(unsafeJson);
        SpecAttestManagerExport export = new(
            "spec-trace-manager.project.v5",
            "manager-project-1",
            7,
            now,
            document.RootElement.Clone());

        Assert.Throws<JsonException>(() => SpecAttestInterchange.FromManagerExport(
            export,
            options,
            "test-version"));
    }

    [TestMethod]
    public async Task Provider_PlansAndAppliesSafeMutationThroughManagerRestAsync()
    {
        SpecAttestProviderOptions options = CreateOptions();
        FakeManagerClient manager = new(CreateManagerExport());
        SpecAttestResourceProvider provider = new(
            manager,
            options,
            new FixedTimeProvider(now));
        SpecAttestInterchangePackage package = SpecAttestInterchange.FromManagerExport(
            CreateManagerExport(),
            options,
            "test-version");
        package = package with
        {
            Manifest = package.Manifest with
            {
                Validation = new SpecAttestInterchangeValidation(
                    CanonicalProfilePassed: true,
                    now,
                    SpecAttestCanonicalJson.Sha256("passed"))
            }
        };
        JsonElement content = JsonSerializer.SerializeToElement(
            package,
            SpecAttestProviderJsonContext.Default.SpecAttestInterchangePackage);
        string digest = SpecAttestCanonicalJson.Sha256(content);
        ResourceChange change = new(
            "change-1",
            ResourceChangeKind.Update,
            new ResourceAddress(SpecAttestInterchangeContract.ResourceType, SetId, SetId),
            content,
            "spec-trace-manager.project.v5:7",
            digest,
            new ResourceProvenance("workbench", now, "tester", "git:abc", digest, "transfer-1", "approval-1"));
        ProviderPlanChangesRequest request = new(
            SpecAttestInterchangeContract.ResourceType,
            SetId,
            options.AuthorityEpoch,
            [change]);

        ProviderPlanChangesResponse plan = await provider.PlanChangesAsync(request);
        ProviderApplyChangesResponse applied = await provider.ApplyChangesAsync(new ProviderApplyChangesRequest(
            SpecAttestInterchangeContract.ResourceType,
            SetId,
            plan.PlanId,
            plan.PlanDigest,
            "apply-1",
            options.AuthorityEpoch,
            new ProviderApproval("tester", "approval-1", now)));

        Assert.AreEqual(ProviderChangeDisposition.Ready, plan.Changes[0].Disposition);
        Assert.AreEqual(ProviderApplyStatus.Applied, applied.Status);
        Assert.IsNotNull(applied.Snapshot);
        Assert.AreEqual("apply-1", manager.LastIdempotencyKey);
        Assert.AreEqual(7, manager.LastStagedRevision);
        CollectionAssert.AreEqual(new[] { "commit" }, manager.ExecutedOperations.ToArray());
    }

    [TestMethod]
    public async Task ManagerClient_RejectsReadWhenLiveStandardPinDiffersAsync()
    {
        string wrongVersion = CreateVersionJson().Replace(
            SpecAttestInterchangeContract.StandardCommit,
            new string('0', 40),
            StringComparison.Ordinal);
        RecordingHandler handler = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/version"] = wrongVersion,
            ["/api/v1/projects/manager-project-1/canonical-export"] = CreateManagerExportJson()
        });
        using HttpClient httpClient = new(handler);
        SpecAttestManagerClient client = new(httpClient, CreateOptions());

        ResourceProviderProtocolException exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(
            () => client.GetCanonicalExportAsync("manager-project-1"));

        Assert.AreEqual("specattest.manager.standard-pin.conflict", exception.Error.Code);
        Assert.HasCount(1, handler.RequestUris);
    }

    [TestMethod]
    public async Task ManagerClient_ExercisesCompleteSafeMutationRestSurfaceAsync()
    {
        RecordingHandler handler = new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/api/v1/interoperability/specattest"] = CreateCapabilitiesJson(),
            ["/api/v1/projects/manager-project-1/interoperability/specattest/mutations/stage"] = CreateOperationJson("staged", 1),
            ["/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1"] = CreateOperationJson("staged", 1),
            ["/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1/commit"] = CreateOperationJson("completed", 2),
            ["/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1/cancel"] = CreateOperationJson("canceled", 2),
            ["/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1/resume"] = CreateOperationJson("completed", 2)
        });
        using HttpClient httpClient = new(handler);
        SpecAttestManagerClient client = new(httpClient, CreateOptions());
        JsonElement stageRequest = JsonSerializer.SerializeToElement(new { test = true });

        SpecAttestSafeMutationOperation staged = await client.StageSafeMutationAsync(
            "manager-project-1",
            stageRequest,
            "idempotency-1");
        _ = await client.GetSafeMutationAsync("manager-project-1", staged.ImportRunId);
        _ = await client.ExecuteSafeMutationAsync("manager-project-1", staged.ImportRunId, "commit", 7, 1);
        _ = await client.ExecuteSafeMutationAsync("manager-project-1", staged.ImportRunId, "cancel", 7, 1);
        _ = await client.ExecuteSafeMutationAsync("manager-project-1", staged.ImportRunId, "resume", 7, 1);

        Assert.AreEqual("idempotency-1", handler.IdempotencyKeys.Single(static value => value is not null));
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "/api/v1/projects/manager-project-1/interoperability/specattest/mutations/stage",
                "/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1",
                "/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1/commit",
                "/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1/cancel",
                "/api/v1/projects/manager-project-1/interoperability/specattest/mutations/import-1/resume"
            },
            handler.RequestUris.Select(static uri => uri.AbsolutePath).ToArray());
        string commitBody = handler.RequestBodies.Single(item => item.Key.EndsWith("/commit", StringComparison.Ordinal)).Value;
        StringAssert.Contains(commitBody, "\"expectedProjectVersion\":7", StringComparison.Ordinal);
        StringAssert.Contains(commitBody, "\"expectedOperationVersion\":1", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ProviderValidation_RejectsUnattestedManagerExportAsync()
    {
        SpecAttestProviderOptions options = CreateOptions();
        SpecAttestResourceProvider provider = new(
            new FakeManagerClient(CreateManagerExport()),
            options,
            new FixedTimeProvider(now));
        ResourceAddress address = new(SpecAttestInterchangeContract.ResourceType, SetId, SetId);
        ResourceDocument document = (await provider.GetAsync(new ProviderGetRequest([address]))).Resources[0];
        ResourceAuthorityDeclaration authority = new(
            SpecAttestInterchangeContract.ResourceType,
            SetId,
            options.AuthorityProviderId,
            options.AuthorityEpoch,
            now,
            DeclarationDigest: null);
        ResourceSnapshot snapshot = new(
            "snapshot-1",
            SpecAttestInterchangeContract.ResourceType,
            SetId,
            now,
            document.PayloadSha256,
            Authoritative: true,
            authority,
            options.StandardPin,
            [document],
            document.Provenance);

        ProviderValidateResponse response = await provider.ValidateAsync(new ProviderValidateRequest([snapshot]));

        Assert.HasCount(1, response.Results);
        Assert.IsFalse(response.Results[0].Valid);
        CollectionAssert.Contains(
            response.Results[0].Diagnostics.Select(static item => item.Code).ToArray(),
            "specattest.interchange.validation.not-attested");
    }

    private static SpecAttestProviderOptions CreateOptions(
        Uri? endpoint = null,
        SpecAttestEndpointMode mode = SpecAttestEndpointMode.Local)
    {
        return new SpecAttestProviderOptions
        {
            Endpoint = endpoint ?? new Uri("http://127.0.0.1:5080"),
            EndpointMode = mode,
            SetId = SetId,
            ManagerProjectId = "manager-project-1",
            AuthorityMode = SpecAttestAuthorityModes.Manager,
            AuthorityEpoch = "manager-epoch-7",
            AuthorityProviderId = SpecAttestResourceProvider.DefaultProviderId,
            StandardPin = CreatePin()
        };
    }

    private static ResourceCompatibilityPin CreatePin()
    {
        return SpecAttestInterchangeContract.StandardPin("core");
    }

    private static SpecAttestManagerExport CreateManagerExport()
    {
        using JsonDocument document = JsonDocument.Parse(CreateManagerExportJson());
        return new SpecAttestManagerExport(
            "spec-trace-manager.project.v5",
            "manager-project-1",
            7,
            now,
            document.RootElement.Clone());
    }

    private static string CreateManagerExportJson()
    {
        return $$"""
            {
              "packageVersion": "spec-trace-manager.project.v5",
              "project": {
                "projectId": "manager-project-1",
                "recordVersion": 7,
                "updatedAt": "{{now:O}}"
              },
              "requirements": [
                {
                  "id": "REQ-DEMO-0001",
                  "revision": 7,
                  "title": "Preserve extensions",
                  "statement": "The adapter MUST preserve extension fields.",
                  "trace": { "supersedes": ["REQ-DEMO-LEGACY-0001"] },
                  "x_extension": { "value": 1 }
                }
              ],
              "artifacts": [
                {
                  "artifact_id": "SPEC-DEMO",
                  "artifact_type": "specification",
                  "title": "Demo"
                }
              ],
              "evidenceSnapshots": [],
              "externalReferences": [
                {
                  "uri": "https://example.test/reference/1",
                  "x_reference_type": "review"
                }
              ],
              "retiredRequirementLedger": { "retired_requirements": [] },
              "history": [
                {
                  "auditEventId": "audit-1",
                  "occurredAt": "{{now:O}}",
                  "operation": "export"
                }
              ]
            }
            """;
    }

    private static string CreateVersionJson()
    {
        JsonObject value = new()
        {
            ["version"] = "0.0.0-ci.test",
            ["standardPin"] = SpecAttestInterchangeContract.ManagerPin(CreatePin())
        };
        return value.ToJsonString();
    }

    private static string CreateCapabilitiesJson()
    {
        JsonObject value = new()
        {
            ["contract"] = SpecAttestInterchangeContract.SafeMutationContract,
            ["standardPin"] = SpecAttestInterchangeContract.ManagerPin(CreatePin()),
            ["operations"] = new JsonArray("stage", "inspect", "commit", "cancel", "resume"),
            ["idempotencyKeysRequired"] = true,
            ["expectedVersionsRequired"] = true,
            ["atomicCommit"] = true,
            ["automaticMerge"] = false,
            ["immutableAuditAndVersions"] = true,
            ["deterministicRetry"] = true
        };
        return value.ToJsonString();
    }

    private static string CreateOperationJson(string status, long version)
    {
        return JsonSerializer.Serialize(new
        {
            importRunId = "import-1",
            projectId = "manager-project-1",
            status,
            recordVersion = version,
            expectedProjectVersion = 7,
            requestDigest = new string('a', 64)
        });
    }

    private sealed class FakeManagerClient(SpecAttestManagerExport export) : ISpecAttestManagerClient
    {
        public string? LastIdempotencyKey { get; private set; }

        public long LastStagedRevision { get; private set; }

        public List<string> ExecutedOperations { get; } = [];

        public Task VerifyStandardPinAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SpecAttestSafeMutationCapabilities> GetSafeMutationCapabilitiesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SpecAttestSafeMutationCapabilities(
                SpecAttestInterchangeContract.SafeMutationContract,
                ["stage", "inspect", "commit", "cancel", "resume"],
                true,
                true,
                true,
                false,
                true,
                true));

        public Task<SpecAttestManagerExport> GetCanonicalExportAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(export);
        }

        public Task<JsonElement> GetIntakePackageAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            using JsonDocument document = JsonDocument.Parse(IntakePackageJson());
            return Task.FromResult(document.RootElement.Clone());
        }

        public Task<SpecAttestSafeMutationOperation> StageSafeMutationAsync(
            string projectId,
            JsonElement stageRequest,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            this.LastIdempotencyKey = idempotencyKey;
            this.LastStagedRevision = stageRequest.GetProperty("package").GetProperty("requirements")[0]
                .GetProperty("revision").GetInt64();
            return Task.FromResult(Operation("staged", 1));
        }

        public Task<SpecAttestSafeMutationOperation> GetSafeMutationAsync(
            string projectId,
            string importRunId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Operation("staged", 1));

        public Task<SpecAttestSafeMutationOperation> ExecuteSafeMutationAsync(
            string projectId,
            string importRunId,
            string operation,
            long expectedProjectVersion,
            long expectedOperationVersion,
            CancellationToken cancellationToken = default)
        {
            this.ExecutedOperations.Add(operation);
            return Task.FromResult(Operation(
                string.Equals(operation, "cancel", StringComparison.Ordinal) ? "canceled" : "completed",
                expectedOperationVersion + 1));
        }

        public Task<SpecAttestManagerStatus> GetStatusAsync(
            bool deep,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpecAttestManagerStatus(ProviderHealthState.Healthy, "test", []));
        }

        private static SpecAttestSafeMutationOperation Operation(string status, long version)
        {
            JsonElement content = JsonSerializer.SerializeToElement(new { status });
            return new SpecAttestSafeMutationOperation(
                "import-1",
                "manager-project-1",
                status,
                version,
                7,
                new string('a', 64),
                content);
        }

        private static string IntakePackageJson()
        {
            return $$"""
                {
                  "packageVersion": "spec-trace-manager.intake.v1",
                  "expectedProjectVersion": 7,
                  "expectedVersions": { "requirement:REQ-DEMO-0001": 3 },
                  "sourceDocuments": [],
                  "requirementGroups": [],
                  "requirements": [
                    {
                      "requirementId": "REQ-DEMO-0001",
                      "revision": 7,
                      "recordVersion": 3,
                      "title": "Preserve extensions",
                      "statement": "The adapter MUST preserve extension fields."
                    }
                  ]
                }
                """;
        }
    }

    private sealed class RecordingHandler(IReadOnlyDictionary<string, string> responses) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        public List<string?> IdempotencyKeys { get; } = [];

        public Dictionary<string, string> RequestBodies { get; } = new(StringComparer.Ordinal);

        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri requestUri = request.RequestUri ?? throw new InvalidOperationException("Request URI was unavailable.");
            this.RequestUris.Add(requestUri);
            this.Authorization = request.Headers.Authorization;
            this.IdempotencyKeys.Add(request.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? values)
                ? values.Single()
                : null);
            if (request.Content is not null)
            {
                this.RequestBodies[requestUri.AbsolutePath] = await request.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses[requestUri.AbsolutePath])
            };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("sensitive transport detail");
        }
    }

    private sealed class ErrorHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(content)
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
