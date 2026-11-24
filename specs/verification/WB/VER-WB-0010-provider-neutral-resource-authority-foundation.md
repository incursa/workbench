---
artifact_id: VER-WB-0010
artifact_type: verification
title: "Provider-neutral resource authority foundation"
domain: WB
status: passed
owner: platform
verifies:
  - REQ-WB-PROVIDER-0001
  - REQ-WB-PROVIDER-0002
  - REQ-WB-PROVIDER-0003
  - REQ-WB-PROVIDER-0004
  - REQ-WB-PROVIDER-0005
  - REQ-WB-PROVIDER-0006
  - REQ-WB-PROVIDER-0007
  - REQ-WB-PROVIDER-0008
related_artifacts:
  - SPEC-WB-RESOURCE-PROVIDERS
  - ARC-WB-0009
  - ARC-WB-0010
  - WI-WB-0028
---

# VER-WB-0010 - Provider-neutral resource authority foundation

## Scope

Configuration compatibility and fail-closed authority validation, protocol
handshake/framing, capability and role enforcement, concurrency/idempotency
guards, batching, structured diagnostics, schemas, and repository build/test
quality.

## Requirements Verified

- [`REQ-WB-PROVIDER-0001`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0002`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0003`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0004`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0005`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0006`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0007`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0008`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

## Verification Method

Automated unit tests, schema/config validation, repository navigation sync,
required formatting, Release build, and solution test execution.

## Procedure or Approach

1. Load a legacy config with no provider section and verify the provider
   configuration defaults to empty without rewriting existing behavior.
2. Validate one authority with optional enricher/publication roles.
3. Verify duplicate scopes, unknown/disabled providers, role overlap,
   unsupported conflict policy, and automatic offline failover fail closed.
4. Send query before handshake and verify a structured handshake-required
   response.
5. Complete handshake and verify v1 capability and role negotiation.
6. Submit update planning without an expected version token and verify the
   provider is not called.
7. Submit apply with mismatched envelope/body idempotency keys and verify the
   provider is not called.
8. Run the stream host with two NDJSON requests and verify one correlated JSON
   response per line with structured diagnostic severity and code.
9. Verify read-only enrichers cannot plan/apply, publication targets cannot
   plan normative writes, and advertised batch limits are enforced before
   transport dispatch.
10. Launch a real test provider process, complete NDJSON handshake/health, then
    force a deadline overrun and verify the process transport terminates.
11. Validate serialized handshake/apply envelopes against the published
    operation-specific JSON Schema and reject a drifted apply body.
12. Run `dotnet format Workbench.slnx`, Release build, and solution tests.

## Expected Result

The additive foundation preserves repositories without provider configuration,
rejects ambiguous authority, isolates provider execution, and exposes a stable
provider-neutral contract without implementing or selecting an adapter.

## Evidence

- `tests/Workbench.Tests/ResourceAuthorityConfigurationTests.cs`
- `tests/Workbench.Tests/ResourceProviderProtocolTests.cs`
- `specs/schemas/resource-authority-config.schema.json`
- `specs/schemas/resource-provider-protocol-v1.schema.json`
- Required format, build, test, and validation commands recorded in the
  implementation handoff.

## Status

passed

## Related Artifacts

- [`SPEC-WB-RESOURCE-PROVIDERS`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
