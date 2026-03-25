---
artifact_id: WI-WB-0004
artifact_type: work_item
title: "Plan repo structure cleanup (reduce large files, improve navigation)"
domain: WB
status: planned
owner: platform
addresses:
  - REQ-CLI-SCAFFOLD-0001
  - REQ-CLI-SCAFFOLD-0002
  - REQ-CLI-SCAFFOLD-0003
  - REQ-CLI-SCAFFOLD-0004
  - REQ-CLI-SCAFFOLD-0005
  - REQ-CLI-MIGRATE-0001
  - REQ-CLI-MIGRATE-0002
  - REQ-CLI-MIGRATE-0003
  - REQ-CLI-MIGRATE-0004
  - REQ-CLI-MIGRATE-0005
  - REQ-CLI-NAV-0001
  - REQ-CLI-NAV-0002
  - REQ-CLI-NAV-0003
  - REQ-CLI-NAV-0004
  - REQ-CLI-NAV-0005
  - REQ-CLI-NAV-0006
  - REQ-WB-STD-0001
  - REQ-WB-STD-0002
  - REQ-WB-STD-0003
  - REQ-WB-STD-0004
design_links:
  - ARC-WB-0007
verification_links:
  - VER-WB-0001
related_artifacts:
  - SPEC-CLI-SCAFFOLD
  - SPEC-CLI-MIGRATE
  - SPEC-CLI-NAV
  - SPEC-WB-STD
  - ARC-WB-0007
  - VER-WB-0001
---

# WI-WB-0004 - Plan repo structure cleanup (reduce large files, improve navigation)

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Create a structured plan to refactor the Workbench codebase into clearer
modules, reduce large file sizes (notably `Program.cs`), and improve overall
navigability without changing behavior.

## Requirements Addressed

- [`REQ-CLI-SCAFFOLD-0001`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0002`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0003`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0004`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0005`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-MIGRATE-0001`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0002`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0003`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0004`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0005`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-NAV-0001`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0002`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0003`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0004`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0005`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0006`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-WB-STD-0001`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0002`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0003`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0004`](../../requirements/WB/SPEC-WB-STD.md)

## Design Inputs

- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

## Planned Changes

- `Program.cs` reduced to <300 lines and contains only wiring.
- Each command has a focused module with a single responsibility.
- Services are grouped by domain with minimal cross-dependencies.
- No behavior changes (CLI output parity verified).

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- [`REQ-CLI-SCAFFOLD-0001`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0002`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0003`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0004`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-SCAFFOLD-0005`](../../requirements/CLI/SPEC-CLI-SCAFFOLD.md)
- [`REQ-CLI-MIGRATE-0001`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0002`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0003`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0004`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-MIGRATE-0005`](../../requirements/CLI/SPEC-CLI-MIGRATE.md)
- [`REQ-CLI-NAV-0001`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0002`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0003`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0004`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0005`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-CLI-NAV-0006`](../../requirements/CLI/SPEC-CLI-NAV.md)
- [`REQ-WB-STD-0001`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0002`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0003`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0004`](../../requirements/WB/SPEC-WB-STD.md)

Uses Design:

- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

Verified By:

- [`VER-WB-0001`](../../verification/WB/VER-WB-0001-repo-operations-and-command-surface.md)
