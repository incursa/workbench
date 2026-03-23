---
artifact_id: SPEC-TUI-TERMINAL-UI
artifact_type: specification
title: Terminal UI Mode
domain: TUI
capability: terminal-ui
status: draft
owner: platform
related_artifacts:
  - WI-WB-0005
  - WI-WB-0006
  - WI-WB-0007
  - ARC-WB-0003
---

# SPEC-TUI-TERMINAL-UI - Terminal UI Mode

## Purpose

Add an interactive terminal UI mode to the Workbench CLI while keeping a
single published executable and reusing the existing service layer.

## Scope

- provide a guided terminal UI for common work-item and doc workflows
- keep the CLI as the source of truth for mutation and validation logic
- keep the executable single-binary friendly
- avoid introducing a separate storage model or backend service

## Context

The TUI is intended to sit between the CLI and a future browser UI: it should
make browsing and common edits easier without changing the data model or
duplicating the underlying business logic.

## REQ-TUI-0001 Expose the TUI from the same executable
The CLI MUST expose the terminal UI as a subcommand or alias inside the same published executable.

Trace:
- Implemented By:
  - [WI-WB-0005](/specs/work-items/WB/WI-WB-0005-plan-terminal-ui-mode.md)
  - [WI-WB-0006](/specs/work-items/WB/WI-WB-0006-implement-shared-core-and-clitui-split.md)
  - [WI-WB-0007](/specs/work-items/WB/WI-WB-0007-build-tui-mvp-with-command-preview-and-dry-run.md)
- Related:
  - [ARC-WB-0003](/architecture/ARC-WB-0003-terminal-ui-mode-in-cli-executable.md)

Notes:
- keep the entrypoint repo-native

## REQ-TUI-0002 Reuse shared services
The TUI MUST use the existing shared services for work-item and doc reads and writes instead of reimplementing parsing or validation.

Trace:
- Implemented By:
  - [WI-WB-0005](/specs/work-items/WB/WI-WB-0005-plan-terminal-ui-mode.md)
  - [WI-WB-0006](/specs/work-items/WB/WI-WB-0006-implement-shared-core-and-clitui-split.md)
  - [WI-WB-0007](/specs/work-items/WB/WI-WB-0007-build-tui-mvp-with-command-preview-and-dry-run.md)
- Related:
  - [ARC-WB-0003](/architecture/ARC-WB-0003-terminal-ui-mode-in-cli-executable.md)

Notes:
- keep mutation logic in shared code
- keep the TUI focused on interaction and presentation

## REQ-TUI-0003 Support core repo workflows
The TUI MUST support browsing work items, viewing details, creating items, changing status or title, opening linked docs, and running sync or validation with progress feedback.

Trace:
- Implemented By:
  - [WI-WB-0005](/specs/work-items/WB/WI-WB-0005-plan-terminal-ui-mode.md)
  - [WI-WB-0006](/specs/work-items/WB/WI-WB-0006-implement-shared-core-and-clitui-split.md)
  - [WI-WB-0007](/specs/work-items/WB/WI-WB-0007-build-tui-mvp-with-command-preview-and-dry-run.md)
- Related:
  - [ARC-WB-0003](/architecture/ARC-WB-0003-terminal-ui-mode-in-cli-executable.md)

Notes:
- keep the command preview visible for mutations
- keep dry-run behavior aligned with the CLI

## REQ-TUI-0004 Stay keyboard-first and state-safe
The TUI MUST support keyboard-first navigation, show the current repo and action state clearly, and restore the terminal cleanly on exit.

Trace:
- Implemented By:
  - [WI-WB-0005](/specs/work-items/WB/WI-WB-0005-plan-terminal-ui-mode.md)
  - [WI-WB-0006](/specs/work-items/WB/WI-WB-0006-implement-shared-core-and-clitui-split.md)
  - [WI-WB-0007](/specs/work-items/WB/WI-WB-0007-build-tui-mvp-with-command-preview-and-dry-run.md)
- Related:
  - [ARC-WB-0003](/architecture/ARC-WB-0003-terminal-ui-mode-in-cli-executable.md)

Notes:
- keep the layout compact
- make the status bar and action hints obvious

## REQ-TUI-0005 Keep command previews explicit
The TUI MUST preview the exact CLI command or equivalent action before any mutation and label dry-run output clearly.

Trace:
- Implemented By:
  - [WI-WB-0005](/specs/work-items/WB/WI-WB-0005-plan-terminal-ui-mode.md)
  - [WI-WB-0006](/specs/work-items/WB/WI-WB-0006-implement-shared-core-and-clitui-split.md)
  - [WI-WB-0007](/specs/work-items/WB/WI-WB-0007-build-tui-mvp-with-command-preview-and-dry-run.md)
- Related:
  - [ARC-WB-0003](/architecture/ARC-WB-0003-terminal-ui-mode-in-cli-executable.md)

Notes:
- keep command previews visible across views
- surface validation messages in UI-friendly dialogs

## Open Questions

- Which TUI framework best balances maturity, design control, and ease of testing?
- Should TUI commands call CLI handlers directly, or should both call a shared command service?
- Do we want `workbench tui` to allow invoking arbitrary CLI commands, or only the supported set?
