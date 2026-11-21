---
artifact_id: WI-WB-0026
artifact_type: work_item
title: "streamline workflow orchestration"
domain: WB
status: in_progress
owner: platform
addresses:
  - REQ-WB-STREAM-0001
  - REQ-WB-STREAM-0002
  - REQ-WB-STREAM-0003
  - REQ-WB-STREAM-0004
  - REQ-WB-STREAM-0005
  - REQ-WB-STREAM-0006
  - REQ-WB-STREAM-0007
  - REQ-WB-STREAM-0008
  - REQ-WB-STREAM-0009
design_links:
  - ARC-WB-0008
verification_links:
  - VER-WB-0009
related_artifacts:
  - SPEC-WB-STREAMLINED-WORKFLOW
---

# WI-WB-0026 - streamline workflow orchestration

## Summary

Reduce Workbench ceremony while preserving its value as durable engineering
intent and project memory.

## Requirements Addressed

- [`REQ-WB-STREAM-0001`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0002`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0003`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0004`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0005`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0006`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0007`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0008`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0009`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)

## Design Inputs

- [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)

## Delivered Changes

- In progress: optional workflow levels and `workbench context`.
- In progress: local-only idempotent `workbench finish`.

## Out of Scope

- Changing existing canonical artifact formats.
- Treating Workbench as authoritative for PR, release, or deployment status.
- Automatic external mutations.
- Removing existing commands before compatibility aliases and migration paths
  exist.

## Verification Plan

- Unit tests for policy resolution, context selection, finish idempotence, and
  evidence deduplication.
- Integration tests for JSON contracts, exit codes, help, generated navigation,
  dry-run behavior, and backward-compatible config loading.
- Full format, build, .NET test, MCP test, and core validation passes.

## Completion Notes

- Work began with the policy/context and finish orchestration foundations.
- Dogfooding confirmed that `nav sync --dry-run` can under-report document
  mutations that the real run performs; separating pure derived-index refresh
  from metadata repair remains a required follow-up before broader automatic
  refresh is enabled.
- Final review hardened terminal-status handling, repository-bound context
  resolution, exact requirement matching, and explicit JSON versioning.
- Review identified that the finish dry-run needed to calculate navigation and
  validation against prospective state and retain downstream stage failures.
- Implemented the prospective dry-run in an isolated mirror and versioned the
  expanded finish JSON contract as schema version 2. Navigation and validation
  failures now retain the applied status/evidence result, stage error, exit
  code, and exact next action.

## Trace Links

Addresses:

- [`SPEC-WB-STREAMLINED-WORKFLOW`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)

Uses Design:

- [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)

Verified By:

- [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)
