---
id: TASK-0006
type: task
status: done
priority: medium
owner: platform
created: 2025-12-30
updated: 2025-12-31
githubSynced: "2025-12-30T22:16:24Z"
tags: []
related:
  specs:
    - /docs/10-product/feature-spec-terminal-ui.md
  adrs:
    - /docs/40-decisions/ADR-2025-12-30-terminal-ui-mode-in-cli-executable.md
  files: []
  prs: []
  issues:
    - "https://github.com/bravellian/workbench/issues/22"
  branches:
    - work/TASK-0006-implement-shared-core-and-clitui-split
title: Implement shared core and CLI/TUI split
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/70-work/items/TASK-0006-implement-shared-core-and-clitui-split.md
---

# TASK-0006 - Implement shared core and CLI/TUI split

## Summary
Split Workbench into core/CLI/TUI projects while preserving a single published
executable and shared command logic.

## Acceptance criteria
- Core project contains shared parsing, validation, and service logic used by CLI and TUI.
- CLI entrypoint dispatches to CLI or TUI modes without duplicating handlers.
- Publish output remains a single-file executable.
- Spec and ADR links are up to date.

## Notes

- started implementation
