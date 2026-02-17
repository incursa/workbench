---
workbench:
  type: adr
  workItems:
    - TASK-0005
    - TASK-0006
    - TASK-0007
  codeRefs: []
  pathHistory: []
  path: /docs/40-decisions/ADR-2025-12-30-terminal-ui-mode-in-cli-executable.md
owner: platform
status: accepted
updated: 2025-12-30
---

# ADR-2025-12-30: Terminal UI mode in CLI executable

- Status: accepted
- Date: 2025-12-30
- Owner: platform

## Context
Workbench uses Markdown as its primary data model with a CLI for manipulation. Users
want a more discoverable interface that still preserves a single executable and the
existing CLI workflows. The UI must show which CLI command was invoked and support
a global dry-run mode that is clearly indicated in outputs.

## Decision
Implement a terminal UI mode as a `workbench tui` subcommand using Terminal.Gui.
Refactor shared logic into a core library so both CLI and TUI reuse the same parsing,
validation, and command execution paths. Publish as a single-file executable that
contains CLI and TUI projects. The TUI must surface the last command invoked and
provide a global dry-run toggle that labels outputs accordingly.

## Alternatives considered
- Separate GUI or web app: rejected due to deployment complexity and duplication.
- Standalone TUI binary: rejected to keep a single executable and shared release flow.
- Directly shelling out to CLI for every action: rejected due to poorer UX and error handling.

## Consequences
- Pros: improved discoverability; single executable; shared logic avoids drift.
- Cons: added dependency and build complexity; larger binary; UI testing effort.

## Related specs
- </docs/10-product/feature-spec-terminal-ui.md>

## Related work items
- [TASK-0005](/docs/70-work/items/TASK-0005-plan-terminal-ui-mode.md)
