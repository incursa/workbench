---
artifact_id: WI-WB-0019
artifact_type: work_item
title: "generate quality report json and markdown summary"
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

# WI-WB-0019 - generate quality report json and markdown summary

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Generate the compared report layer that puts authored testing intent beside
observed evidence and records evidence gaps without turning that report into a
policy gate.

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

- Workbench emits [`artifacts/quality/testing/quality-report.json`](../../../artifacts/quality/testing/quality-report.json) and
  [`artifacts/quality/testing/quality-summary.md`](../../../artifacts/quality/testing/quality-summary.md).
- The report contains authored, observed, and assessment sections as separate
  structures.
- Detectable gaps are explicit, structured, and grounded in current evidence.

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
