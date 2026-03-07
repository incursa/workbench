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
    - /docs/10-product/feature-spec-quality-evidence-testing-v1.md
  adrs:
    - /docs/40-decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  files:
    - /docs/30-contracts/quality-evidence-model.md
  prs: []
  issues: []
  branches: []
---

# TASK-0019 - Generate quality report JSON and Markdown summary

## Summary

Generate the compared report layer that puts authored testing intent beside
observed evidence and records evidence gaps without turning that report into a
policy gate.

## Acceptance criteria

- Workbench emits `artifacts/quality/testing/quality-report.json` and
  `artifacts/quality/testing/quality-summary.md`.
- The report contains authored, observed, and assessment sections as separate
  structures.
- Detectable gaps are explicit, structured, and grounded in current evidence.
