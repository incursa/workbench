---
artifact_id: SPEC-WB-STREAMLINED-WORKFLOW
artifact_type: specification
title: "Streamlined intent and execution workflow"
domain: WB
capability: workflow-orchestration
status: draft
owner: platform
related_artifacts:
  - ARC-WB-0008
  - WI-WB-0026
  - VER-WB-0009
---

# SPEC-WB-STREAMLINED-WORKFLOW - Streamlined intent and execution workflow

## Purpose

Keep Workbench authoritative for durable engineering intent while reducing the
administrative work required to reconcile that intent with GitHub and deployed
reality.

## Scope

- proportional workflow policy
- agent context and next-action guidance
- idempotent completion orchestration
- generated navigation maintenance
- deterministic repository repair
- external execution-state reconciliation
- cross-repository initiative relationships
- stable automation contracts and consolidated CI

## Context

Repository-local specifications, architecture, work items, and verification
evidence are valuable project memory. Friction occurs when those artifacts are
treated as a second execution tracker or require manual housekeeping after the
authoritative PR, release, or deployment has already completed.

## REQ-WB-STREAM-0001 Proportional workflow policy

Workbench MUST support `light`, `standard`, and `governed` workflow levels,
including a low-ceremony path for trivial changes and emergency hotfixes.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0002 One idempotent finish operation

Workbench MUST provide one idempotent finish operation that records completion
evidence, updates local intent state and backlinks, refreshes derived views,
validates the applicable policy, and keeps external synchronization explicit.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0003 Generated navigation is automatically maintained

Workbench MUST treat indexes and navigation as derived data that orchestration
commands refresh automatically after mutating canonical artifacts.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0004 Deterministic doctor repairs

`workbench doctor --fix` MUST limit automatic repairs to deterministic local
changes while returning one exact next action when manual intervention is
required.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0005 Concise agent context

`workbench context` MUST return the applicable repository policy, active work,
specifications, architecture, constraints, and exactly one recommended next
command in stable machine-readable output.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0006 Cross-repository initiative relationships

Workbench SHOULD represent stable initiative and parent-child relationships
without copying execution status between repositories.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0007 External execution state remains authoritative

Workbench MUST allow GitHub and deployment systems to remain authoritative for
merge, release, and deployment state while retaining engineering intent and
traceability locally.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0008 Stable automation contract

New orchestration commands MUST use stable JSON envelopes, documented exit
codes, concise human output, and idempotent local mutations.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)

## REQ-WB-STREAM-0009 Consolidated validation path

Workbench SHOULD provide one fast CI entry point that composes required checks
without duplicating equivalent build, validation, or evidence work.

Trace:
- Satisfied By:
  - [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
- Implemented By:
  - [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- Verified By:
  - [`VER-WB-0009`](../../verification/WB/VER-WB-0009-streamlined-workflow-orchestration.md)
