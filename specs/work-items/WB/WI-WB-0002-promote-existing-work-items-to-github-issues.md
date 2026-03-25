---
artifact_id: WI-WB-0002
artifact_type: work_item
title: "Sync work items with GitHub issues and branches"
domain: WB
status: complete
owner: platform
addresses:
  - REQ-SYNC-0001
  - REQ-SYNC-0002
  - REQ-SYNC-0003
  - REQ-SYNC-0004
  - REQ-SYNC-0005
design_links:
  - ARC-WB-0002
  - ARC-WB-0004
verification_links:
  - VER-WB-0001
related_artifacts:
  - SPEC-SYNC-WORK-ITEM-SYNC
  - ARC-WB-0002
  - ARC-WB-0004
  - VER-WB-0001
---

# WI-WB-0002 - Sync work items with GitHub issues and branches

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Add a sync command that keeps local work items and GitHub issues aligned
without deletes. Sync should create missing issues or local work items and
optionally create branches for items that need them.

## Requirements Addressed

- [`REQ-SYNC-0001`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0002`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0003`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0004`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0005`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)

## Design Inputs

- [`ARC-WB-0002`](../../architecture/WB/ARC-WB-0002-github-provider-abstraction-and-octokit-default.md)
- [`ARC-WB-0004`](../../architecture/WB/ARC-WB-0004-repo-native-operating-model.md)

## Planned Changes

- Sync is two-way and never deletes local work items or GitHub issues.
- If a local work item is missing a GitHub issue, sync creates it.
- If a GitHub issue is missing a local work item, sync creates it.
- Sync can create a branch for an item when one is missing.
- Work item metadata records the GitHub issue reference and branch name.
- Closed local items do not create GitHub issues or branches.
- GitHub issues always import into local work items, regardless of issue state.
- ID-scoped sync supports a `--prefer` flag to pick local or GitHub as the source of truth.
- Bulk sync defaults to pushing local content to GitHub when descriptions differ.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- [`REQ-SYNC-0001`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0002`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0003`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0004`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0005`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)

Uses Design:

- [`ARC-WB-0002`](../../architecture/WB/ARC-WB-0002-github-provider-abstraction-and-octokit-default.md)
- [`ARC-WB-0004`](../../architecture/WB/ARC-WB-0004-repo-native-operating-model.md)

Verified By:

- [`VER-WB-0001`](../../verification/WB/VER-WB-0001-repo-operations-and-command-surface.md)
