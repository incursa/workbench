using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
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
    public async Task ManagerClient_ReadsCanonicalExportAndUsesBearerAuthenticationAsync()
    {
        RecordingHandler handler = new(CreateManagerExportJson());
        using HttpClient httpClient = new(handler);
        SpecAttestProviderOptions options = CreateOptions() with { AccessToken = "test-token" };
        SpecAttestManagerClient client = new(httpClient, options);

        SpecAttestManagerExport result = await client.GetCanonicalExportAsync("manager-project-1");

        Assert.AreEqual("spec-trace-manager.project.v5", result.PackageVersion);
        Assert.AreEqual("manager-project-1", result.ProjectId);
        Assert.AreEqual(7, result.RecordVersion);
        Assert.AreEqual(
            new Uri("http://127.0.0.1:5080/api/v1/projects/manager-project-1/canonical-export"),
            handler.RequestUri);
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
        Assert.AreEqual(CreatePin(), first.Resources[0].CompatibilityPin);
        Assert.HasCount(1, first.Diagnostics);
        Assert.AreEqual("specattest.validation.not-attested", first.Diagnostics[0].Code);
        CollectionAssert.AreEqual(
            new[] { ResourceProviderRole.ReadOnlyEnricher },
            capabilities.Roles.ToArray());
        CollectionAssert.DoesNotContain(capabilities.Operations.ToArray(), ResourceProviderOperations.PlanChanges);

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
    public async Task Provider_RejectsMutationBecauseManagerLacksSafeInterchangeApplyAsync()
    {
        SpecAttestProviderOptions options = CreateOptions();
        SpecAttestResourceProvider provider = new(
            new FakeManagerClient(CreateManagerExport()),
            options,
            new FixedTimeProvider(now));
        ProviderPlanChangesRequest request = new(
            SpecAttestInterchangeContract.ResourceType,
            SetId,
            options.AuthorityEpoch,
            []);

        ResourceProviderProtocolException exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(
            () => provider.PlanChangesAsync(request));

        Assert.AreEqual("specattest.manager.safe-mutation.unavailable", exception.Error.Code);
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
        return new ResourceCompatibilityPin(
            SpecAttestInterchangeContract.StandardPinContract,
            "https://github.com/incursa/spec-trace",
            "test-fixture",
            new string('a', 40),
            "model/model.schema.json",
            new string('b', 64),
            SpecAttestInterchangeContract.Canonicalization,
            "core");
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
                  "title": "Preserve extensions",
                  "statement": "The adapter MUST preserve extension fields.",
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

    private sealed class FakeManagerClient(SpecAttestManagerExport export) : ISpecAttestManagerClient
    {
        public Task<SpecAttestManagerExport> GetCanonicalExportAsync(
            string projectId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(export);
        }

        public Task<SpecAttestManagerStatus> GetStatusAsync(
            bool deep,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpecAttestManagerStatus(ProviderHealthState.Healthy, "test", []));
        }
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.RequestUri = request.RequestUri;
            this.Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });
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
