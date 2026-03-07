---
id: TASK-0018
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

# TASK-0018 - Add Cobertura ingestion and coverage summary contract

## Summary

Normalize coverage outputs into a stable coverage-summary artifact that can
compare authored thresholds and critical files against observed coverage.

## Acceptance criteria

- Workbench ingests Cobertura-compatible coverage files and emits
  `artifacts/quality/testing/coverage-summary.json`.
- The summary includes overall coverage plus per-file and critical-file views.
- The output matches `docs/30-contracts/coverage-summary.schema.json`.
