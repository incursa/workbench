---
id: TASK-0020
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

# TASK-0020 - Add quality sync and show command surface

## Summary

Implement the small V1 CLI surface for the subsystem and keep it aligned with
Workbench's existing sync/show and JSON-envelope conventions.

## Acceptance criteria

- `workbench quality sync` emits normalized artifacts and a standard JSON
  summary envelope.
- `workbench quality show` defaults to the latest quality report and can return
  selected artifacts in table or JSON form.
- No extra V1 subcommands are introduced unless they are strictly required by
  the implementation.
