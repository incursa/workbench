---
id: TASK-0007
type: task
status: draft
priority: medium
owner: platform
created: 2025-12-30
updated: null
githubSynced: "2026-02-17T04:53:16Z"
tags: []
related:
  specs:
    - /docs/10-product/feature-spec-terminal-ui.md
  adrs:
    - /docs/40-decisions/ADR-2025-12-30-terminal-ui-mode-in-cli-executable.md
  files: []
  prs: []
  issues:
    - "https://github.com/bravellian/workbench/issues/571"
  branches: []
title: Build TUI MVP with command preview and dry-run
---

# TASK-0007 - Build TUI MVP with command preview and dry-run

## Summary
Build the initial Terminal.Gui-based TUI with work item workflows, command preview,
and a global dry-run toggle for discoverability.

## Acceptance criteria
- TUI provides basic work item workflows defined in the spec (list, view, create, update).
- Bottom status line shows the last command invoked by the TUI.
- Global dry-run toggle is visible and marks outputs as dry-run.
- Errors surface CLI validation messages in dialogs.
- Spec and ADR links are up to date.
