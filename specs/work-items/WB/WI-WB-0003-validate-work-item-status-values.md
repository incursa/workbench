---
artifact_id: WI-WB-0003
artifact_type: work_item
title: "Validate work item status values"
domain: WB
status: planned
owner: platform
addresses:
  - REQ-CLI-ITEM-STATUS-0001
  - REQ-CLI-ITEM-STATUS-0002
  - REQ-CLI-ITEM-STATUS-0003
  - REQ-CLI-ITEM-STATUS-0004
  - REQ-CLI-ITEM-STATUS-0005
  - REQ-CLI-ITEM-STATUS-0006
  - REQ-CLI-ITEM-STATUS-0007
  - REQ-CLI-ITEM-0001
  - REQ-CLI-ITEM-0002
  - REQ-CLI-ITEM-0003
  - REQ-CLI-ITEM-0004
  - REQ-CLI-ITEM-0005
  - REQ-CLI-ITEM-0006
  - REQ-CLI-ITEM-0007
design_links:
  - ARC-WB-0007
verification_links:
  - VER-WB-0001
related_artifacts:
  - SPEC-CLI-ITEM-STATUS
  - SPEC-CLI-ITEM
  - ARC-WB-0007
  - VER-WB-0001
---

# WI-WB-0003 - Validate work item status values

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Prevent setting work item statuses that are not in the allowed set. Add a
future-friendly path for configurable statuses.

## Requirements Addressed

- [`REQ-CLI-ITEM-STATUS-0001`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0002`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0003`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0004`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0005`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0006`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0007`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-0001`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0002`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0003`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0004`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0005`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0006`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0007`](../../requirements/CLI/SPEC-CLI-ITEM.md)

## Design Inputs

- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

## Planned Changes

- `workbench item status` rejects invalid status values with a clear error.
- `workbench item new` and `workbench item import` validate status overrides.
- Validation logic is centralized so future configuration can override defaults.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- [`REQ-CLI-ITEM-STATUS-0001`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0002`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0003`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0004`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0005`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0006`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-STATUS-0007`](../../requirements/CLI/SPEC-CLI-ITEM-STATUS.md)
- [`REQ-CLI-ITEM-0001`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0002`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0003`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0004`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0005`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0006`](../../requirements/CLI/SPEC-CLI-ITEM.md)
- [`REQ-CLI-ITEM-0007`](../../requirements/CLI/SPEC-CLI-ITEM.md)

Uses Design:

- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

Verified By:

- [`VER-WB-0001`](../../verification/WB/VER-WB-0001-repo-operations-and-command-surface.md)
