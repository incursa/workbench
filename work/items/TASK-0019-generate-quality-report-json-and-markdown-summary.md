---
id: TASK-0019
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
title: generate quality report json and markdown summary
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /work/items/TASK-0019-generate-quality-report-json-and-markdown-summary.md
---

# TASK-0019 - generate quality report json and markdown summary

## Summary

Generate the compared report layer that puts authored testing intent beside
observed evidence and records evidence gaps without turning that report into a
policy gate.

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

- Workbench emits `artifacts/quality/testing/quality-report.json` and
  `artifacts/quality/testing/quality-summary.md`.
- The report contains authored, observed, and assessment sections as separate
  structures.
- Detectable gaps are explicit, structured, and grounded in current evidence.

## Notes

-
