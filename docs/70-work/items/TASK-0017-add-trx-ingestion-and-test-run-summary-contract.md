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
    - /docs/10-product/feature-spec-quality-evidence-testing-v1.md
  adrs:
    - /docs/40-decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  files:
    - /docs/30-contracts/quality-evidence-model.md
  prs: []
  issues: []
  branches: []
---

# TASK-0017 - Add TRX ingestion and test run summary contract

## Summary

Normalize TRX outputs into a stable run-summary artifact that captures what ran,
what passed, what failed, and what was skipped.

## Acceptance criteria

- Workbench ingests one or more TRX files and emits
  `artifacts/quality/testing/test-run-summary.json`.
- The summary retains enough per-test identity to compare run results against
  inventory and required tests.
- The output matches `docs/30-contracts/test-run-summary.schema.json`.
