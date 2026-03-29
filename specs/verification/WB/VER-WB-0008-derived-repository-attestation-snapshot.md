---
artifact_id: VER-WB-0008
artifact_type: verification
title: "Derived repository attestation snapshot"
domain: WB
status: passed
owner: platform
verifies:
  - REQ-CLI-QUALITY-ATTEST-0001
  - REQ-CLI-QUALITY-ATTEST-0002
  - REQ-CLI-QUALITY-ATTEST-0003
  - REQ-CLI-QUALITY-ATTEST-0004
  - REQ-CLI-QUALITY-ATTEST-0005
  - REQ-CLI-QUALITY-ATTEST-0006
related_artifacts:
  - SPEC-CLI-QUALITY-ATTEST
  - WI-WB-0025
  - ARC-WB-0005
---

# VER-WB-0008 - Derived repository attestation snapshot

## Scope

Derived attestation command behavior, static semantic HTML output, JSON
snapshot shape, scoped runs, explicit evidence execution, and graceful partial
adoption handling.

## Requirements Verified

- [`REQ-CLI-QUALITY-ATTEST-0001`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0002`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0003`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0004`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0005`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`REQ-CLI-QUALITY-ATTEST-0006`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)

## Verification Method

Repository validation, CLI integration tests, and attestation service unit
tests.

## Preconditions

- The local repository contains the attestation command surface and spec leaf.

## Procedure or Approach

1. Regenerate and check the CLI help snapshot against the live source tree.
2. Run scoped auditable validation for the new attestation spec.
3. Execute the attestation unit and integration tests that exercise JSON and
   HTML output.

## Expected Result

The attestation command emits derived reports and evidence snapshots without
mutating canonical trace artifacts.

## Evidence

- [`tests/Workbench.IntegrationTests/AttestationCommandTests.cs`](../../../tests/Workbench.IntegrationTests/AttestationCommandTests.cs)
- [`tests/Workbench.Tests/AttestationServiceTests.cs`](../../../tests/Workbench.Tests/AttestationServiceTests.cs)
- [`specs/requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`specs/generated/commands.md`](../../generated/commands.md)
- [`specs/generated/test-matrix.md`](../../generated/test-matrix.md)

## Status

The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.

passed

## Related Artifacts

- [`SPEC-CLI-QUALITY-ATTEST`](../../requirements/CLI/SPEC-CLI-QUALITY-ATTEST.md)
- [`WI-WB-0025`](../../work-items/WB/WI-WB-0025-derive-repository-attestation-snapshot.md)
- [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)
