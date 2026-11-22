---
artifact_id: WI-WB-0027
artifact_type: work_item
title: "complete web editing and connected execution capabilities"
domain: WB
status: in_progress
owner: platform
addresses:
  - REQ-WEB-0012
  - REQ-WEB-0014
  - REQ-WB-STREAM-0006
  - REQ-WB-STREAM-0007
  - REQ-WB-STREAM-0008
design_links:
  - ARC-WB-0006
  - ARC-WB-0008
verification_links:
  - VER-WB-0007
  - VER-WB-0009
related_artifacts:
  - SPEC-WEB-LOCAL-UI
  - SPEC-WB-STREAMLINED-WORKFLOW
priority: high
---

# WI-WB-0027 - complete web editing and connected execution capabilities

## Summary

Finish the supported requirement-card editing workflow, add opt-in read-only
external execution observations, introduce backward-compatible initiative and
parent relationships, and continue evidence-driven trace migration.

## Requirements Addressed

- [`REQ-WEB-0012`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0014`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WB-STREAM-0006`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0007`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0008`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)

## Design Inputs

- [`ARC-WB-0006`](../../architecture/WB/ARC-WB-0006-local-web-ui-mode.md)
- [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)

## Planned Changes

- Add Move Earlier, Move Later, and atomic Save All handlers and controls to
  the requirement editor.
- Add stable integration coverage for ordering, invalid batches, concurrent
  source changes, and all-or-nothing persistence.
- Add a provider-neutral, read-only execution observation contract and an
  explicit GitHub-backed refresh path with offline fake-provider coverage.
- Add optional, versioned parent and initiative references that preserve
  existing work items and independent-clone behavior.
- Regenerate derived navigation for the new hierarchy without importing remote
  execution state.
- Re-run traceable and auditable validation, fix only unambiguous metadata, and
  improve guidance for the remaining evidence backlog.

## Out of Scope

- Automatic external mutations, deployments, retries, or status propagation.
- Mandatory migration of existing repositories or work items.
- Synthetic trace links added only to satisfy a validation profile.

## Verification Plan

- Extend [`VER-WB-0007`](../../verification/WB/VER-WB-0007-compact-spec-browser-and-requirement-cards.md)
  with production-handler ordering and atomicity evidence.
- Extend [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)
  with provider, snapshot, hierarchy, versioned JSON, and no-implicit-network
  evidence.
- Run formatting, the full .NET suite, MCP tests when touched, core validation,
  dependency audits, and remote CI.

## Completion Notes

The work item was created after PR #616 merged so the follow-up remains
separate from the completed readiness-remediation history.

- Completed the requirement-editor slice with persisted Move Earlier and Move
  Later controls, a batch editor with atomic Save All, and optimistic source
  revisions that reject stale overwrites.
- Added production-host integration coverage for both move directions, valid
  batch persistence, byte-stable invalid-batch rejection, and concurrent source
  changes.
- Added an explicit `execution refresh` boundary backed by a GET-only GitHub
  provider for pull requests, releases, and deployments, plus an offline
  `execution show` path over versioned derived snapshots.
- Provider failures preserve matching prior observations and report the refresh
  error, prior observation timestamp, and age without changing canonical work
  items. Unit and CLI integration tests cover missing credentials, normalized
  provider state, snapshot versioning, and offline reads.
- Added optional schema-version-2 `parent_ref` and `initiative_refs` fields,
  versioned `item hierarchy` JSON commands, offline cross-repository URI
  validation, local self/cycle/missing-reference rejection, and derived
  navigation. Existing work items remain valid without migration.

## Trace Links

Addresses:

- [`REQ-WEB-0012`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0014`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WB-STREAM-0006`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0007`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0008`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)

Uses Design:

- [`ARC-WB-0006`](../../architecture/WB/ARC-WB-0006-local-web-ui-mode.md)
- [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)

Verified By:

- [`VER-WB-0007`](../../verification/WB/VER-WB-0007-compact-spec-browser-and-requirement-cards.md)
- [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)
