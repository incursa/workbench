---
artifact_id: WI-WB-0029
artifact_type: work_item
title: "implement first-party CSV, SpecAttest, and ClickUp resource providers"
domain: WB
status: complete
owner: platform
addresses:
  - REQ-WB-PROVIDER-0009
  - REQ-WB-PROVIDER-0010
  - REQ-WB-PROVIDER-0011
design_links:
  - ARC-WB-0009
  - ARC-WB-0010
verification_links:
  - VER-WB-0011
related_artifacts:
  - SPEC-WB-RESOURCE-PROVIDERS
priority: high
---

# WI-WB-0029 - implement first-party CSV, SpecAttest, and ClickUp resource providers

## Summary

Implement three out-of-process adapters on the provider-neutral v1 contract:
local CSV authority/editable projection, read-only SpecAttest Manager
interchange, and ClickUp execution resources with explicitly bounded mutation.

## Requirements Addressed

- [`REQ-WB-PROVIDER-0009`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0010`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0011`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

## Design Inputs

- [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- [`SPEC-001` accepted authority package](../../../../PF-M0.4-workbench-specattest-authority-decision-package.md)

## Completed Changes

- Added versioned CSV schemas and copy-ready templates, an authority provider,
  editable projection preview/reconciliation, exact authority coordinates,
  cross-process mutation serialization, and durable retry receipts.
- Added the read-only SpecAttest Manager adapter, exact Standard-pin and
  authority validation, deterministic interchange JSON/ZIP packaging, retired
  ledger and path validation, and an explicit fail-closed mutation boundary.
- Added the ClickUp execution-resource adapter with read-only/plan/apply modes,
  configured-List enforcement, mapped-field-only mutation, create-marker
  reconciliation, archive-only delete, immutable plans, durable receipts, and
  cross-process serialization.
- Added protocol-level mutation invariants for deadlines, payloads, version
  tokens, transfer records, plan digests, and publication-plan session binding.
- Added operator reference documentation, complete configuration examples,
  repository scaffolding, and focused automated coverage.

## Out of Scope

- Selecting the deferred SpecAttest Standard tuple or claiming cross-tool
  parity, write interoperability, publication readiness, or production cutover.
- Treating SpecAttest Manager as a publication target or mutation authority.
- Removing ClickUp's remote compare-before-write race, which its API cannot
  eliminate without a conditional-update primitive.
- Live-service mutation tests; repository verification uses local files,
  subprocesses, mock HTTP handlers, and temporary state directories.

## Verification

- [`VER-WB-0011`](../../verification/WB/VER-WB-0011-first-party-resource-provider-adapters.md)

## Trace Links

Addresses:

- [`SPEC-WB-RESOURCE-PROVIDERS`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

Uses Design:

- [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)

Verified By:

- [`VER-WB-0011`](../../verification/WB/VER-WB-0011-first-party-resource-provider-adapters.md)
