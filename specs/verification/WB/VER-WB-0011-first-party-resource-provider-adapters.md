---
artifact_id: VER-WB-0011
artifact_type: verification
title: "First-party CSV, SpecAttest, and ClickUp resource providers"
domain: WB
status: passed
owner: platform
verifies:
  - REQ-WB-PROVIDER-0009
  - REQ-WB-PROVIDER-0010
  - REQ-WB-PROVIDER-0011
related_artifacts:
  - SPEC-WB-RESOURCE-PROVIDERS
  - ARC-WB-0009
  - ARC-WB-0010
  - WI-WB-0029
---

# VER-WB-0011 - First-party CSV, SpecAttest, and ClickUp resource providers

## Scope

Provider layering, authority semantics, protocol invariants, CSV round-trip and
reconciliation safety, SpecAttest exact-pin read-only interchange, ClickUp
scope and mutation safety, schemas, templates, documentation, and repository
quality gates.

## Requirements Verified

- [`REQ-WB-PROVIDER-0009`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0010`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`REQ-WB-PROVIDER-0011`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

## Verification Method

Automated unit and integration tests, mocked HTTP handlers, temporary local
state, schema and CSV parsing, Spec Trace repository validation, documentation
and navigation checks, formatting, Release build, and solution test execution.

## Procedure or Approach

1. Verify provider executables use the shared v1 host and are isolated from
   Workbench core through configured process transport without dynamic assembly
   loading.
2. Verify the shared client and host reject missing deadlines, malformed
   digests, invalid payload/version combinations, incomplete authority
   transfers, stale epochs, role violations, and direct publication apply.
3. Exercise CSV authority plan/apply, stale file and epoch conflicts, durable
   same-key retry snapshots, explicit projection operations, multiline and
   Unicode round-trip, and row-schema validation.
4. Exercise SpecAttest authority and Standard-pin validation, unattested export
   behavior, deterministic interchange and ZIP output, retired-ID ledger and
   path safety, digest failures, and unavailable mutation operations.
5. Exercise ClickUp pagination and mapped reads, configured-List enforcement,
   immutable single-item plans, conflicts, archive-only delete, create-marker
   reconciliation, ambiguous outcomes, durable receipts, and credential-safe
   diagnostics using mock HTTP handlers only.
6. Parse every JSON Schema, validate representative provider protocol/config
   documents, inspect both CSV templates as empty header-only RFC 4180 files,
   and run repository documentation and navigation dry-runs.
7. Run required formatting, Release build, full solution tests, core profile,
   Spec Trace repository validation, diff checks, and quality evidence refresh.

## Expected Result

All three adapters honor the declared-authority contract without bypassing the
out-of-process protocol. CSV authority and ClickUp bounded mutation are explicit
and retry-safe within their documented limits. SpecAttest remains truthfully
read-only and unattested until upstream contracts and the owner-selected
Standard tuple permit stronger claims.

## Evidence

- `tests/Workbench.Tests/ResourceProviderProtocolTests.cs`
- `tests/Workbench.Tests/CsvResourceProviderTests.cs`
- `tests/Workbench.Providers.ClickUp.Tests/`
- `tests/Workbench.SpecAttestProvider.Tests/`
- `specs/schemas/resource-provider-protocol-v1.schema.json`
- `specs/schemas/csv-work-item-row-v1.schema.json`
- `specs/schemas/csv-specification-row-v1.schema.json`
- Required command results recorded in the implementation handoff.

## Status

passed

## Related Artifacts

- [`SPEC-WB-RESOURCE-PROVIDERS`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
- [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- [`WI-WB-0029`](../../work-items/WB/WI-WB-0029-first-party-resource-provider-adapters.md)
