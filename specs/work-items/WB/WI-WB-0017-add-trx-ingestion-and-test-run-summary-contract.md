---
artifact_id: WI-WB-0017
artifact_type: work_item
title: "add trx ingestion and test run summary contract"
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

# WI-WB-0017 - add trx ingestion and test run summary contract

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Normalize TRX outputs into a stable run-summary artifact that captures what ran,
what passed, what failed, and what was skipped.

## Requirements Addressed

- REQ-QE-0001
- REQ-QE-0002
- REQ-QE-0003
- REQ-QE-0004
- REQ-QE-0005
- REQ-QE-0006

## Design Inputs

- ARC-WB-0005

## Planned Changes

- Workbench ingests one or more TRX files and emits
- `artifacts/quality/testing/test-run-summary.json`.
- The summary retains enough per-test identity to compare run results against
- inventory and required tests.
- The output matches `schemas/test-run-summary.schema.json`.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- REQ-QE-0001
- REQ-QE-0002
- REQ-QE-0003
- REQ-QE-0004
- REQ-QE-0005
- REQ-QE-0006

Uses Design:

- ARC-WB-0005

Verified By:

- VER-WB-0002
