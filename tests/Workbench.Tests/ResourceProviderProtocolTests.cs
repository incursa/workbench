using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public sealed class ResourceProviderProtocolTests
{
    private static readonly DateTimeOffset now = new(2026, 11, 21, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Host_RequiresHandshakeAndEnforcesConcurrencyAndIdempotencyAsync()
    {
        var provider = new FakeResourceProvider();
        var host = new ResourceProviderProtocolHost(provider, new FixedTimeProvider(now));

        var beforeHandshake = await host.HandleAsync(CreateRequest(
            "query-before-handshake",
            ResourceProviderOperations.Query,
            new ProviderQueryRequest("specification", "set-1", null, 10, null),
            ProviderProtocolJsonContext.Default.ProviderQueryRequest));
        Assert.IsFalse(beforeHandshake.Ok);
        Assert.AreEqual("provider.protocol.handshake.required", beforeHandshake.Error?.Code);

        var handshake = await host.HandleAsync(CreateRequest(
            "handshake",
            ResourceProviderOperations.Handshake,
            new ProviderHandshakeRequest("test", "1.0.0", [ResourceProviderProtocol.Version1]),
            ProviderProtocolJsonContext.Default.ProviderHandshakeRequest));
        Assert.IsTrue(handshake.Ok);

        var malformedBody = await host.HandleAsync(new ProviderRequestEnvelope(
            ResourceProviderProtocol.Version1,
            "malformed-plan",
            ResourceProviderOperations.PlanChanges,
            now.AddMinutes(1),
            IdempotencyKey: null,
            JsonSerializer.SerializeToElement(new { })));
        Assert.IsFalse(malformedBody.Ok);
        Assert.AreEqual("provider.protocol.body.invalid", malformedBody.Error?.Code);

        var missingVersion = await host.HandleAsync(CreateRequest(
            "plan",
            ResourceProviderOperations.PlanChanges,
            new ProviderPlanChangesRequest(
                "specification",
                "set-1",
                "epoch-1",
                [CreateChange(ResourceChangeKind.Update, expectedVersionToken: null)]),
            ProviderProtocolJsonContext.Default.ProviderPlanChangesRequest));
        Assert.IsFalse(missingVersion.Ok);
        Assert.AreEqual("provider.concurrency.version-token.required", missingVersion.Error?.Code);
        Assert.AreEqual(0, provider.PlanCallCount);

        var missingTransferRecord = await host.HandleAsync(CreateRequest(
            "transfer",
            ResourceProviderOperations.PlanChanges,
            new ProviderPlanChangesRequest(
                "specification",
                "set-1",
                "epoch-1",
                [CreateChange(ResourceChangeKind.TransferAuthority, "version-1")]),
            ProviderProtocolJsonContext.Default.ProviderPlanChangesRequest));
        Assert.IsFalse(missingTransferRecord.Ok);
        Assert.AreEqual("provider.authority-transfer.required", missingTransferRecord.Error?.Code);
        Assert.AreEqual(0, provider.PlanCallCount);

        var expiredDeadline = await host.HandleAsync(new ProviderRequestEnvelope(
            ResourceProviderProtocol.Version1,
            "expired",
            ResourceProviderOperations.Health,
            now,
            IdempotencyKey: null,
            JsonSerializer.SerializeToElement(
                new ProviderHealthRequest(),
                ProviderProtocolJsonContext.Default.ProviderHealthRequest)));
        Assert.IsFalse(expiredDeadline.Ok);
        Assert.AreEqual("provider.protocol.deadline.exceeded", expiredDeadline.Error?.Code);

        var apply = new ProviderApplyChangesRequest(
            "specification",
            "set-1",
            "plan-1",
            Hash('a'),
            "body-key",
            "epoch-1",
            new ProviderApproval("owner", "approval:1", now));
        var mismatchedIdempotency = await host.HandleAsync(CreateRequest(
            "apply",
            ResourceProviderOperations.ApplyChanges,
            apply,
            ProviderProtocolJsonContext.Default.ProviderApplyChangesRequest,
            idempotencyKey: "envelope-key"));
        Assert.IsFalse(mismatchedIdempotency.Ok);
        Assert.AreEqual("provider.idempotency.key.invalid", mismatchedIdempotency.Error?.Code);
        Assert.AreEqual(0, provider.ApplyCallCount);
    }

    [TestMethod]
    public async Task Host_UsesOneLineJsonAndReturnsStructuredDiagnosticsAsync()
    {
        var provider = new FakeResourceProvider();
        var host = new ResourceProviderProtocolHost(provider, new FixedTimeProvider(now));
        var handshake = CreateRequest(
            "handshake",
            ResourceProviderOperations.Handshake,
            new ProviderHandshakeRequest("test", "1.0.0", [ResourceProviderProtocol.Version1]),
            ProviderProtocolJsonContext.Default.ProviderHandshakeRequest);
        var invalidBatch = CreateRequest(
            "get",
            ResourceProviderOperations.Get,
            new ProviderGetRequest([]),
            ProviderProtocolJsonContext.Default.ProviderGetRequest);
        using var input = new StringReader(
            JsonSerializer.Serialize(handshake, ProviderProtocolJsonContext.Default.ProviderRequestEnvelope)
            + Environment.NewLine
            + JsonSerializer.Serialize(invalidBatch, ProviderProtocolJsonContext.Default.ProviderRequestEnvelope)
            + Environment.NewLine);
        using var output = new StringWriter();

        await host.RunAsync(input, output);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(2, lines);
        var response = JsonSerializer.Deserialize(lines[1], ProviderProtocolJsonContext.Default.ProviderResponseEnvelope);
        Assert.IsNotNull(response);
        Assert.IsFalse(response.Ok);
        Assert.AreEqual("provider.batch.size.invalid", response.Error?.Code);
        Assert.HasCount(1, response.Diagnostics);
        Assert.AreEqual(ProviderDiagnosticSeverity.Error, response.Diagnostics[0].Severity);
    }

    [TestMethod]
    public async Task Client_EnforcesConfiguredRoleAndAdvertisedBatchLimitAsync()
    {
        var transport = new FakeTransport(CreateCapabilities(ResourceProviderRole.ReadOnlyEnricher));
        await using var client = await ResourceProviderClient.ConnectAsync(
            transport,
            CreateConfiguration(ResourceProviderRole.ReadOnlyEnricher),
            "specification",
            "set-1",
            "fake",
            "1.0.0",
            TimeSpan.FromSeconds(5),
            new FixedTimeProvider(now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PlanChangesAsync(
            new ProviderPlanChangesRequest(
                "specification",
                "set-1",
                "epoch-1",
                [CreateChange(ResourceChangeKind.Create, expectedVersionToken: null)])));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(
            new ProviderGetRequest(
            [
                new ResourceAddress("specification", "set-1", "1"),
                new ResourceAddress("specification", "set-1", "2"),
                new ResourceAddress("specification", "set-1", "3")
            ])));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.QueryAsync(
            new ProviderQueryRequest("specification", "set-1", null, 3, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.QueryAsync(
            new ProviderQueryRequest("specification", "another-set", null, 1, null)));
        Assert.HasCount(1, transport.Requests);
        Assert.AreEqual(ResourceProviderOperations.Handshake, transport.Requests[0].Operation);
    }

    [TestMethod]
    public async Task Client_PublicationTargetRejectsNormativeWritesAsync()
    {
        var transport = new FakeTransport(CreateCapabilities(ResourceProviderRole.PublicationTarget));
        await using var client = await ResourceProviderClient.ConnectAsync(
            transport,
            CreateConfiguration(ResourceProviderRole.PublicationTarget),
            "specification",
            "set-1",
            "fake",
            "1.0.0",
            TimeSpan.FromSeconds(5),
            new FixedTimeProvider(now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PlanChangesAsync(
            new ProviderPlanChangesRequest(
                "specification",
                "set-1",
                "epoch-1",
                [CreateChange(ResourceChangeKind.Update, "version-1")])));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ApplyChangesAsync(
            new ProviderApplyChangesRequest(
                "specification",
                "set-1",
                "authority-plan",
                Hash('a'),
                "publication-key",
                "epoch-1",
                new ProviderApproval("owner", "approval:publication", now))));
        Assert.HasCount(1, transport.Requests);
    }

    [TestMethod]
    public async Task Client_RejectsMutationProviderWithoutSafetyCapabilitiesAsync()
    {
        var capabilities = CreateCapabilities(ResourceProviderRole.Authority) with
        {
            Batch = new ProviderBatchCapabilities(2, AtomicApply: false, OrderedResults: true),
            SupportsIdempotency = false,
            SupportsOptimisticConcurrency = false
        };
        var transport = new FakeTransport(capabilities);

        var exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            ResourceProviderClient.ConnectAsync(
                transport,
                CreateConfiguration(ResourceProviderRole.Authority),
                "specification",
                "set-1",
                "fake",
                "1.0.0",
                TimeSpan.FromSeconds(5),
                new FixedTimeProvider(now)));

        Assert.AreEqual("provider.protocol.concurrency.required", exception.Error.Code);
    }

    [TestMethod]
    public void BaselineAndPublicationReceipt_RoundTripImmutableCoordinates()
    {
        var provenance = new ResourceProvenance(
            "publisher",
            now,
            "owner",
            "revision-1",
            Hash('c'),
            "transfer-1",
            "approval:1");
        var baseline = new ResourceBaseline("baseline-1", "snapshot-1", Hash('c'), now, provenance);
        var receipt = new ResourcePublicationReceipt(
            "publication-1",
            "publisher",
            baseline.SnapshotId,
            baseline.SnapshotPayloadSha256,
            Hash('d'),
            now,
            "specattest://publication/1",
            provenance);

        var baselineJson = JsonSerializer.Serialize(baseline, ProviderProtocolJsonContext.Default.ResourceBaseline);
        var receiptJson = JsonSerializer.Serialize(receipt, ProviderProtocolJsonContext.Default.ResourcePublicationReceipt);
        var parsedBaseline = JsonSerializer.Deserialize(baselineJson, ProviderProtocolJsonContext.Default.ResourceBaseline);
        var parsedReceipt = JsonSerializer.Deserialize(receiptJson, ProviderProtocolJsonContext.Default.ResourcePublicationReceipt);

        Assert.AreEqual("snapshot-1", parsedBaseline?.SnapshotId);
        Assert.AreEqual(Hash('c'), parsedBaseline?.SnapshotPayloadSha256);
        Assert.AreEqual(Hash('d'), parsedReceipt?.ManifestSha256);
        Assert.AreEqual("specattest://publication/1", parsedReceipt?.ImmutableReference);
    }

    [TestMethod]
    public void ProtocolSchema_ValidatesOperationBodiesAndRejectsShapeDrift()
    {
        var repoRoot = FindRepoRoot();
        const string SchemaPath = "specs/schemas/resource-provider-protocol-v1.schema.json";
        var handshake = CreateRequest(
            "handshake",
            ResourceProviderOperations.Handshake,
            new ProviderHandshakeRequest("test", "1.0.0", [ResourceProviderProtocol.Version1]),
            ProviderProtocolJsonContext.Default.ProviderHandshakeRequest);
        var apply = CreateRequest(
            "apply",
            ResourceProviderOperations.ApplyChanges,
            new ProviderApplyChangesRequest(
                "specification",
                "set-1",
                "plan-1",
                Hash('a'),
                "key-1",
                "epoch-1",
                new ProviderApproval("owner", "approval:1", now)),
            ProviderProtocolJsonContext.Default.ProviderApplyChangesRequest,
            "key-1");
        var invalidApply = apply with { Body = JsonSerializer.SerializeToElement(new { planId = "plan-1" }) };

        var handshakeErrors = SchemaValidationService.ValidateJsonContent(
            repoRoot,
            SchemaPath,
            "handshake",
            JsonSerializer.Serialize(handshake, ProviderProtocolJsonContext.Default.ProviderRequestEnvelope));
        var applyErrors = SchemaValidationService.ValidateJsonContent(
            repoRoot,
            SchemaPath,
            "apply",
            JsonSerializer.Serialize(apply, ProviderProtocolJsonContext.Default.ProviderRequestEnvelope));
        var invalidErrors = SchemaValidationService.ValidateJsonContent(
            repoRoot,
            SchemaPath,
            "invalid-apply",
            JsonSerializer.Serialize(invalidApply, ProviderProtocolJsonContext.Default.ProviderRequestEnvelope));

        Assert.IsEmpty(handshakeErrors, string.Join(Environment.NewLine, handshakeErrors));
        Assert.IsEmpty(applyErrors, string.Join(Environment.NewLine, applyErrors));
        Assert.IsNotEmpty(invalidErrors);
    }

    [TestMethod]
    public async Task ProcessTransport_UsesRealNdjsonProtocolAndTerminatesOnTimeoutAsync()
    {
        var repoRoot = FindRepoRoot();
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var buildConfiguration = outputDirectory.Parent?.Name
            ?? throw new DirectoryNotFoundException("Could not determine test build configuration.");
        var targetFramework = outputDirectory.Name;
        var providerAssembly = Path.Combine(
            repoRoot,
            "tests",
            "Workbench.ProviderTestHost",
            "bin",
            buildConfiguration,
            targetFramework,
            "Workbench.ProviderTestHost.dll");
        Assert.IsTrue(File.Exists(providerAssembly), $"Provider test host was not built: {providerAssembly}");
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var configuration = new ResourceProvidersConfig
        {
            // A real dotnet child process can take more than one second to
            // cold-start on a loaded Windows runner. Keep the timeout test
            // deterministic without making process startup itself the subject.
            DefaultTimeoutSeconds = 5,
            Providers =
            [
                new ResourceProviderProcessConfig
                {
                    Id = "test-process",
                    Executable = dotnetHost,
                    Arguments = [providerAssembly],
                    TimeoutSeconds = 5
                }
            ],
            ResourceSets =
            [
                new ResourceAuthorityBindingConfig
                {
                    ResourceType = "specification",
                    ResourceSet = "set-1",
                    AuthorityProvider = "test-process"
                }
            ]
        };

        await using var client = await ResourceProviderClient.ConnectConfiguredProcessAsync(
            configuration,
            repoRoot,
            "specification",
            "set-1",
            "test-process",
            "1.0.0");
        var health = await client.GetHealthAsync(new ProviderHealthRequest(Deep: false));
        Assert.AreEqual(ProviderHealthState.Healthy, health.State);

        var exception = await Assert.ThrowsAsync<ResourceProviderProtocolException>(() =>
            client.GetHealthAsync(new ProviderHealthRequest(Deep: true)));
        Assert.AreEqual("provider.protocol.timeout", exception.Error.Code);
    }

    private static ProviderCapabilities CreateCapabilities(ResourceProviderRole role)
    {
        return new ProviderCapabilities(
            "fake",
            "Fake provider",
            "1.0.0",
            [ResourceProviderProtocol.Version1],
            ResourceProviderOperations.All,
            ["specification"],
            [role],
            new ProviderBatchCapabilities(2, AtomicApply: true, OrderedResults: true),
            SupportsIdempotency: true,
            SupportsOptimisticConcurrency: true,
            SupportsOfflineSnapshots: true);
    }

    private static ResourceProvidersConfig CreateConfiguration(ResourceProviderRole role)
    {
        var providers = new List<ResourceProviderProcessConfig>
        {
            new() { Id = "fake", Executable = "fake-provider" }
        };
        if (role != ResourceProviderRole.Authority)
        {
            providers.Add(new ResourceProviderProcessConfig { Id = "authority", Executable = "authority-provider" });
        }

        return new ResourceProvidersConfig
        {
            Providers = providers,
            ResourceSets =
            [
                new ResourceAuthorityBindingConfig
                {
                    ResourceType = "specification",
                    ResourceSet = "set-1",
                    AuthorityProvider = role == ResourceProviderRole.Authority ? "fake" : "authority",
                    ReadOnlyEnrichers = role == ResourceProviderRole.ReadOnlyEnricher ? ["fake"] : [],
                    PublicationTargets = role == ResourceProviderRole.PublicationTarget ? ["fake"] : []
                }
            ]
        };
    }

    private static ResourceChange CreateChange(ResourceChangeKind kind, string? expectedVersionToken)
    {
        var content = JsonSerializer.SerializeToElement(new { title = "Example" });
        return new ResourceChange(
            "change-1",
            kind,
            new ResourceAddress("specification", "set-1", "SPEC-1"),
            content,
            expectedVersionToken,
            Hash('b'),
            new ResourceProvenance("fake", now, "actor", "revision", Hash('b'), null, null));
    }

    private static ProviderRequestEnvelope CreateRequest<T>(
        string requestId,
        string operation,
        T body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        string? idempotencyKey = null)
    {
        return new ProviderRequestEnvelope(
            ResourceProviderProtocol.Version1,
            requestId,
            operation,
            now.AddMinutes(1),
            idempotencyKey,
            JsonSerializer.SerializeToElement(body, typeInfo));
    }

    private static string Hash(char value) => new(value, 64);

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

    private sealed class FakeTransport(ProviderCapabilities capabilities) : IResourceProviderTransport
    {
        public List<ProviderRequestEnvelope> Requests { get; } = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<ProviderResponseEnvelope> SendAsync(
            ProviderRequestEnvelope request,
            CancellationToken cancellationToken = default)
        {
            this.Requests.Add(request);
            if (!string.Equals(request.Operation, ResourceProviderOperations.Handshake, StringComparison.Ordinal))
            {
                throw new AssertFailedException($"Unexpected transport request: {request.Operation}");
            }

            var body = JsonSerializer.SerializeToElement(
                new ProviderHandshakeResponse(ResourceProviderProtocol.Version1, capabilities),
                ProviderProtocolJsonContext.Default.ProviderHandshakeResponse);
            return Task.FromResult(new ProviderResponseEnvelope(
                ResourceProviderProtocol.Version1,
                request.RequestId,
                Ok: true,
                body,
                [],
                Error: null));
        }
    }

    private sealed class FakeResourceProvider : IResourceProvider
    {
        public int PlanCallCount { get; private set; }

        public int ApplyCallCount { get; private set; }

        public Task<ProviderApplyChangesResponse> ApplyChangesAsync(
            ProviderApplyChangesRequest request,
            CancellationToken cancellationToken = default)
        {
            this.ApplyCallCount++;
            return Task.FromResult(new ProviderApplyChangesResponse(
                ProviderApplyStatus.Applied,
                request.PlanId,
                request.IdempotencyKey,
                request.ExpectedAuthorityEpoch,
                Snapshot: null,
                []));
        }

        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderCapabilities(
                "fake",
                "Fake provider",
                "1.0.0",
                [ResourceProviderProtocol.Version1],
                ResourceProviderOperations.All,
                ["specification"],
                [ResourceProviderRole.Authority],
                new ProviderBatchCapabilities(10, AtomicApply: true, OrderedResults: true),
                SupportsIdempotency: true,
                SupportsOptimisticConcurrency: true,
                SupportsOfflineSnapshots: true));
        }

        public Task<ProviderGetResponse> GetAsync(
            ProviderGetRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderGetResponse([], []));
        }

        public Task<ProviderHealthResponse> GetHealthAsync(
            ProviderHealthRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResponse(
                ProviderHealthState.Healthy,
                now,
                "1.0.0",
                []));
        }

        public Task<ProviderPlanChangesResponse> PlanChangesAsync(
            ProviderPlanChangesRequest request,
            CancellationToken cancellationToken = default)
        {
            this.PlanCallCount++;
            return Task.FromResult(new ProviderPlanChangesResponse(
                "plan-1",
                Hash('a'),
                request.ExpectedAuthorityEpoch,
                now,
                now.AddMinutes(5),
                RequiresExplicitApproval: true,
                [],
                []));
        }

        public Task<ProviderQueryResponse> QueryAsync(
            ProviderQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderQueryResponse([], null, []));
        }

        public Task<ProviderValidateResponse> ValidateAsync(
            ProviderValidateRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderValidateResponse([]));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
