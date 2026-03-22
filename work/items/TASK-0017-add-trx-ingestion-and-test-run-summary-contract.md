---
id: TASK-0017
type: task
status: draft
priority: high
owner: platform
created: 2026-03-07
updated: null
githubSynced: null
tags:
  - quality
  - testing
  - phase-1
related:
  specs:
    - /specs/SPEC-QA-QUALITY-EVIDENCE.md
  adrs:
    - /decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  files:
    - /contracts/quality-evidence-model.md
    - /specs/SPEC-QA-QUALITY-EVIDENCE.md
    - /decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  prs: []
  issues: []
  branches: []
title: add trx ingestion and test run summary contract
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /work/items/TASK-0017-add-trx-ingestion-and-test-run-summary-contract.md
---

# TASK-0017 - add trx ingestion and test run summary contract

## Summary

Normalize TRX outputs into a stable run-summary artifact that captures what ran,
what passed, what failed, and what was skipped.

## Context

-

## Traceability

- Requirement IDs: []
- Architecture docs: []
- Verification docs: []
- Related contracts or runbooks: []

## Implementation notes

-

## Acceptance criteria

- Workbench ingests one or more TRX files and emits
  `artifacts/quality/testing/test-run-summary.json`.
- The summary retains enough per-test identity to compare run results against
  inventory and required tests.
- The output matches `schemas/test-run-summary.schema.json`.

## Notes

-
