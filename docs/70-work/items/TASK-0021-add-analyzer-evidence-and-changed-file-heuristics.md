---
id: TASK-0021
type: task
status: draft
priority: medium
owner: platform
created: 2026-03-07
updated: null
githubSynced: null
tags:
  - quality
  - testing
  - phase-2
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

# TASK-0021 - Add analyzer evidence and changed-file heuristics

## Summary

Extend the quality evidence subsystem beyond test execution by ingesting
analyzer/static-check outputs and flagging changed files with weak nearby
evidence.

## Acceptance criteria

- Analyzer/static-check evidence becomes a distinct observed evidence kind.
- Changed-file heuristics surface likely blind spots without pretending they are
  policy failures.
- The quality report keeps analyzer evidence separate from authored testing
  intent and from test results.
