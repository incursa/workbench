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
    - /specs/SPEC-QA-QUALITY-EVIDENCE.md
  adrs:
    - /docs/40-decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  files:
    - /docs/30-contracts/quality-evidence-model.md
  prs: []
  issues: []
  branches: []
title: add quality sync and show command surface
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/70-work/items/TASK-0020-add-quality-sync-and-show-command-surface.md"
  path: /docs/70-work/items/TASK-0020-add-quality-sync-and-show-command-surface.md
---

# TASK-0020 - add quality sync and show command surface

## Summary

Implement the small V1 CLI surface for the subsystem and keep it aligned with
Workbench's existing sync/show and JSON-envelope conventions.

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

- `workbench quality sync` emits normalized artifacts and a standard JSON
  summary envelope.
- `workbench quality show` defaults to the latest quality report and can return
  selected artifacts in table or JSON form.
- No extra V1 subcommands are introduced unless they are strictly required by
  the implementation.

## Notes

-
