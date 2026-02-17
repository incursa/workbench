---
id: TASK-0025
type: task
status: ready
priority: medium
owner: null
created: 2026-02-17
updated: null
githubSynced: "2026-02-17T04:50:40Z"
tags: []
related:
  specs: []
  adrs: []
  files: []
  prs: []
  issues:
    - "https://github.com/bravellian/workbench/issues/559"
  branches: []
title: Build TUI MVP with command preview and dry-run
---

# TASK-0025 - Build TUI MVP with command preview and dry-run

## Summary

Imported from GitHub issue: https://github.com/bravellian/workbench/issues/559

## Summary
Build the initial Terminal.Gui-based TUI with work item workflows, command preview,
and a global dry-run toggle for discoverability.

## Acceptance criteria
- TUI provides basic work item workflows defined in the spec (list, view, create, update).
- Bottom status line shows the last command invoked by the TUI.
- Global dry-run toggle is visible and marks outputs as dry-run.
- Errors surface CLI validation messages in dialogs.
- Spec and ADR links are up to date.

## Related
- Specs:
  - [feature-spec-terminal-ui](https://github.com/bravellian/workbench/blob/main/docs/10-product/feature-spec-terminal-ui.md)
- ADRs:
  - [ADR-2025-12-30-terminal-ui-mode-in-cli-executable](https://github.com/bravellian/workbench/blob/main/docs/40-decisions/ADR-2025-12-30-terminal-ui-mode-in-cli-executable.md)

## Acceptance criteria
-
