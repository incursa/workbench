---
artifact_id: WI-WB-0025
artifact_type: work_item
title: "derive repository attestation snapshot"
domain: WB
status: complete
owner: platform
addresses:
  - REQ-CLI-QUALITY-ATTEST-0001
  - REQ-CLI-QUALITY-ATTEST-0002
  - REQ-CLI-QUALITY-ATTEST-0003
  - REQ-CLI-QUALITY-ATTEST-0004
  - REQ-CLI-QUALITY-ATTEST-0005
  - REQ-CLI-QUALITY-ATTEST-0006
design_links:
  - ARC-WB-0005
verification_links:
  - VER-WB-0008
related_artifacts:
  - SPEC-CLI-QUALITY-ATTEST
  - ARC-WB-0005
  - VER-WB-0008
---

# WI-WB-0025 - derive repository attestation snapshot

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Implement the derived repository evidence attestation command that produces
static summary and detailed HTML reports plus a machine-readable JSON snapshot,
while keeping the report separate from canonical Spec Trace artifacts.

## Requirements Addressed

- [`REQ-CLI-QUALITY-ATTEST-0001`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0002`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0003`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0004`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0005`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0006`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)

## Design Inputs

- [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

## Delivered Changes

- `workbench quality attest` emits a derived repository attestation snapshot.
- The command writes summary HTML, detailed HTML, and JSON outputs under
  `artifacts/quality/attestation/`.
- The report separates canonical trace, direct refs, work-item status,
  verification status, and current evidence health.
- Evidence refresh execution remains explicit and opt-in.

## Out of Scope

- Changes to the canonical Spec Trace standard.
- Automatic mutation of requirements, work items, or verification artifacts.

## Verification Plan

Proved by the attestation unit tests, CLI integration tests, generated help
snapshot parity, and scoped auditable validation checks.

## Completion Notes

Implemented in the current worktree.

## Trace Links

Addresses:

- [`REQ-CLI-QUALITY-ATTEST-0001`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0002`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0003`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0004`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0005`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0006`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)

Uses Design:

- [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Verified By:

- [`VER-WB-0008`](../../verification/WB/VER-WB-0008-derived-repository-attestation-snapshot.md)
