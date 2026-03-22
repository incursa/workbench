---
id: TASK-0023
type: task
status: draft
priority: high
owner: platform
created: 2026-03-20
updated: null
githubSynced: null
tags: []
related:
  specs:
    - /specs/SPEC-WEB-LOCAL-UI.md
  adrs:
    - /docs/40-decisions/ADR-2026-03-20-local-web-ui-mode.md
  files:
    - /specs/SPEC-WEB-LOCAL-UI.md
    - /docs/40-decisions/ADR-2026-03-20-local-web-ui-mode.md
  prs: []
  issues: []
  branches: []
title: build local web ui mode for workbench
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /work/items/TASK-0023-build-local-web-ui-mode-for-workbench.md
---

# TASK-0023 - build local web ui mode for workbench

## Summary

Add a local browser-based UI that runs from the same Workbench executable and reuses the existing core file-backed services for work items, docs, navigation sync, and validation.

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

- `workbench web` starts a local browser UI from the Workbench executable.
- The UI reuses the same `Workbench.Core` file-backed services instead of duplicating business logic.
- Users can browse work items, inspect details, edit a selected item, and create a new item on a separate page.
- Users can browse local docs and repo files using structured tree views and inline markdown previews.
- Users can trigger repo sync and validation actions from the UI.
- The UI works in single-file publish mode and serves its static assets from the application bundle.
- The UI exposes a local settings page for storing an authoring profile.
- Documentation and architecture decisions are linked to the work item.

## Notes

-
