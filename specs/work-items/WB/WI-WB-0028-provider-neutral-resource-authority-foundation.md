---
artifact_id: WI-WB-0028
artifact_type: work_item
title: "implement provider-neutral resource authority foundation"
domain: WB
status: complete
owner: platform
addresses:
  - REQ-WB-PROVIDER-0001
  - REQ-WB-PROVIDER-0002
  - REQ-WB-PROVIDER-0003
  - REQ-WB-PROVIDER-0004
  - REQ-WB-PROVIDER-0005
  - REQ-WB-PROVIDER-0006
  - REQ-WB-PROVIDER-0007
  - REQ-WB-PROVIDER-0008
design_links:
  - ARC-WB-0009
  - ARC-WB-0010
verification_links:
  - VER-WB-0010
related_artifacts:
  - SPEC-WB-RESOURCE-PROVIDERS
priority: high
---

# WI-WB-0028 - implement provider-neutral resource authority foundation

## Summary

Implement the generic resource/provider vocabulary, configurable one-writer
authority model, process-isolated v1 protocol, client/host abstractions,
schemas, and focused verification required before adapter-specific work.

## Requirements Addressed

- [`REQ-WB-PROVIDER-0001`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0002`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0003`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0004`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0005`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0006`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0007`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0008`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

## Design Inputs

- [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- [`SPEC-001` accepted authority package](../../../../PF-M0.4-workbench-specattest-authority-decision-package.md)

## Completed Changes

- Added optional backward-compatible provider and authority configuration.
- Added fail-closed validation for exact scopes, one authority, valid role
  references, role overlap, conflict policy, and store-and-forward policy.
- Added the provider-neutral resource, snapshot, baseline, projection,
  publication receipt, compatibility pin, provenance, authority declaration,
  and authority-transfer records.
- Added the versioned request/response envelope, capability negotiation,
  query/get/validate/plan/apply/health contracts, batch semantics, structured
  diagnostics, idempotency, approval, authority epoch, and version tokens.
- Added a typed client that enforces advertised capabilities and configured
  roles before dispatch.
- Added an executable process transport with direct argument passing, bounded
  stderr, deadlines, cancellation, and process-tree termination.
- Added the common provider host used by first-party and other provider
  executables.
- Added JSON schemas to repository scaffolding and the root Workbench config
  schema.
- Added focused configuration, schema, protocol, host, and client tests plus a
  real process-host test for NDJSON framing and timeout termination.

## Out of Scope

- CSV, ClickUp, SpecAttest, or other adapter executables.
- Selecting the deferred SpecAttest Standard tuple.
- Claiming Workbench/Manager parity, authority transfer completion,
  publication readiness, or production cutover.
- Replacing current GitHub-specific compatibility abstractions.
- Provider credential storage or distribution.

## Verification

- [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## Trace Links

Addresses:

- [`SPEC-WB-RESOURCE-PROVIDERS`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

Uses Design:

- [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)

Verified By:

- [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)
