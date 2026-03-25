---
artifact_id: ARC-WB-0003
artifact_type: architecture
title: "Terminal UI mode in CLI executable"
domain: WB
status: approved
owner: platform
satisfies:
  - REQ-TUI-0001
  - REQ-TUI-0002
  - REQ-TUI-0003
  - REQ-TUI-0004
  - REQ-TUI-0005
related_artifacts:
  - SPEC-TUI-TERMINAL-UI
  - WI-WB-0005
  - WI-WB-0006
  - WI-WB-0007
  - VER-WB-0003
---

# ARC-WB-0003 - Terminal UI mode in CLI executable

## Purpose

Workbench uses Markdown as its primary data model with a CLI for manipulation. Users
want a more discoverable interface that still preserves a single executable and the
existing CLI workflows. The UI must show which CLI command was invoked and support
a global dry-run mode that is clearly indicated in outputs.
The current shipped CLI does not expose this mode yet; this architecture
documents the planned contract and its shared-service boundary.

## Requirements Satisfied

- [`REQ-TUI-0001`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0002`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0003`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0004`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0005`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)

## Design Summary

Implement a terminal UI mode as a `workbench tui` subcommand using Terminal.Gui.
Refactor shared logic into a core library so both CLI and TUI reuse the same parsing,
validation, and command execution paths. Publish as a single-file executable that
contains CLI and TUI projects. The TUI must surface the last command invoked and
provide a global dry-run toggle that labels outputs accordingly.

## Key Components

- Single published executable that hosts both CLI and TUI entrypoints
- Shared core services for parsing, validation, and mutation logic
- Command preview before mutation
- Global dry-run toggle that is visible in output

## Data and State Considerations

The TUI must reuse the same repository state and command execution paths as the CLI so interaction and automation stay aligned.

## Edge Cases and Constraints

- Terminal state must be restored cleanly on exit.
- Keyboard-first navigation must remain usable across views.
- The TUI should not introduce a second storage model.

## Alternatives Considered

- Separate GUI or web app: rejected due to deployment complexity and duplication.
- Standalone TUI binary: rejected to keep a single executable and shared release flow.
- Directly shelling out to CLI for every action: rejected due to poorer UX and error handling.

## Risks

- Pros: improved discoverability; single executable; shared logic avoids drift.
- Cons: added dependency and build complexity; larger binary; UI testing effort.

## Open Questions

- None.
