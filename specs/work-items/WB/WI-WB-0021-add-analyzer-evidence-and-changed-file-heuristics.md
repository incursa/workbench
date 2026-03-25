---
artifact_id: WI-WB-0021
artifact_type: work_item
title: "add analyzer evidence and changed file heuristics"
domain: WB
status: planned
owner: platform
addresses:
  - REQ-QE-0001
  - REQ-QE-0002
  - REQ-QE-0003
  - REQ-QE-0004
  - REQ-QE-0005
  - REQ-QE-0006
design_links:
  - ARC-WB-0005
verification_links:
  - VER-WB-0002
related_artifacts:
  - SPEC-QA-QUALITY-EVIDENCE
  - ARC-WB-0005
  - VER-WB-0002
---

# WI-WB-0021 - add analyzer evidence and changed file heuristics

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Extend the quality evidence subsystem beyond test execution by ingesting
analyzer/static-check outputs and flagging changed files with weak nearby
evidence.

## Requirements Addressed

- [`REQ-QE-0001`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0002`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0003`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0004`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0005`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0006`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)

## Design Inputs

- [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

## Planned Changes

- Analyzer/static-check evidence becomes a distinct observed evidence kind.
- Changed-file heuristics surface likely blind spots without pretending they are
- policy failures.
- The quality report keeps analyzer evidence separate from authored testing
- intent and from test results.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- [`REQ-QE-0001`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0002`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0003`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0004`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0005`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [`REQ-QE-0006`](../../requirements/QA/SPEC-QA-QUALITY-EVIDENCE.md)

Uses Design:

- [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Verified By:

- [`VER-WB-0002`](../../verification/WB/VER-WB-0002-quality-evidence.md)
