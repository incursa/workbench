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

- Delivered: optional workflow levels and versioned `workbench context`.
- Delivered: local-only idempotent `workbench finish`, including prospective
  dry-run and structured downstream stage results.
- Delivered: deterministic `workbench doctor --fix` scaffolding and
  established-index repair with versioned JSON and one next action.

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
- Updated the docs MCP dependency graph and removed stale security overrides;
  `npm audit` now reports zero vulnerabilities and all MCP tests pass.
- Formalized the docs MCP as a public, unauthenticated, read-only surface and
  added request/query bounds, security/cache headers, and negative transport
  tests without introducing runtime state or mutation.
- Expanded doctor coverage to GitHub, OpenAI, Codex, voice transcription, and
  generated navigation. Automatic repair remains local-only, refuses malformed
  configuration, and does not create index files a repository has not adopted.

## Traceability Migration Backlog

The validator now treats Markdown artifact links and plain IDs equivalently.
After removing duplicate clauses, stale external-local references, and two body
mismatches, the remaining findings are genuine authoring work rather than
parser noise:

- traceable: 444 requirements without a downstream relationship
- auditable: the same 444 downstream gaps, 479 missing verification links,
  453 reciprocal mismatches, and 14 orphan artifacts

Migration order is reciprocal-claim review, real verification evidence, then
unimplemented requirement disposition. Do not bulk-link generic artifacts to
make the profiles green.

## Trace Links

Addresses:

- [`REQ-WB-STREAM-0001`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0002`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0003`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0004`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0005`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0006`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0007`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0008`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0009`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)

Uses Design:

- [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)

Verified By:

- [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)
