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
title: add cobertura ingestion and coverage summary contract
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /work/items/TASK-0018-add-cobertura-ingestion-and-coverage-summary-contract.md
---

# TASK-0018 - add cobertura ingestion and coverage summary contract

## Summary

Normalize coverage outputs into a stable coverage-summary artifact that can
compare authored thresholds and critical files against observed coverage.

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

- Workbench ingests Cobertura-compatible coverage files and emits
  `artifacts/quality/testing/coverage-summary.json`.
- The summary includes overall coverage plus per-file and critical-file views.
- The output matches `schemas/coverage-summary.schema.json`.

## Notes

-
