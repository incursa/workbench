---
artifact_id: WI-WB-0006
artifact_type: work_item
title: "Implement shared core and CLI/TUI split"
domain: WB
status: complete
owner: platform
addresses:
  - REQ-TUI-0001
  - REQ-TUI-0002
  - REQ-TUI-0003
  - REQ-TUI-0004
  - REQ-TUI-0005
design_links:
  - ARC-WB-0003
  - ARC-WB-0007
verification_links:
  - VER-WB-0003
related_artifacts:
  - SPEC-TUI-TERMINAL-UI
  - ARC-WB-0003
  - ARC-WB-0007
  - VER-WB-0003
---

# WI-WB-0006 - Implement shared core and CLI/TUI split

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Split Workbench into core/CLI/TUI projects while preserving a single published
executable and shared command logic.

## Requirements Addressed

- [`REQ-TUI-0001`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0002`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0003`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0004`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0005`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)

## Design Inputs

- [`ARC-WB-0003`](../../architecture/WB/ARC-WB-0003-terminal-ui-mode-in-cli-executable.md)
- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

## Planned Changes

- Core project contains shared parsing, validation, and service logic used by CLI and TUI.
- CLI entrypoint dispatches to CLI or TUI modes without duplicating handlers.
- Publish output remains a single-file executable.
- Spec and architecture links are up to date.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- [`REQ-TUI-0001`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0002`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0003`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0004`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0005`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)

Uses Design:

- [`ARC-WB-0003`](../../architecture/WB/ARC-WB-0003-terminal-ui-mode-in-cli-executable.md)
- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

Verified By:

- [`VER-WB-0003`](../../verification/WB/VER-WB-0003-terminal-ui-mode.md)
