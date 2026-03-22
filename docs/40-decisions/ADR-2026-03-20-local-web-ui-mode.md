---
workbench:
  type: adr
  workItems:
    - TASK-0023
  codeRefs: []
  pathHistory:
    - "C:/docs/40-decisions/ADR-2026-03-20-local-web-ui-mode.md"
  path: /docs/40-decisions/ADR-2026-03-20-local-web-ui-mode.md
owner: platform
status: proposed
updated: 2026-03-20
---

# ADR-2026-03-20: Local web UI mode in the Workbench executable

- Status: proposed
- Date: 2026-03-20
- Owner: platform

## Context

Workbench already has a CLI and a TUI, but the most ergonomic human editing experience is often a browser-based form and list workflow. GitHub issues are convenient for PM-style triage, but the authoritative repo-local state lives in markdown files and front matter. We need a local UI that feels easier than memorizing commands while still reusing the same file-backed logic and remaining single-file friendly.

## Decision

Add a browser-based local UI mode to the existing Workbench executable using ASP.NET Core Razor Pages. The web UI will run as another mode of the same binary, use the existing `Workbench.Core` services for reads and writes, and ship with embedded static assets so the published tool can remain a single-file artifact. The first pass includes a compact overview page, work-item browser/editor, a separate create page, docs browser with rendered markdown and tree grouping, a repo file explorer, and a machine-local author profile page.

## Alternatives considered

- VS Code extension: rejected as the primary surface because it requires a specific editor and marketplace distribution.
- Electron desktop app: rejected for now because it would add a larger packaging and maintenance burden than the UI problem warrants.
- Separate web service: rejected because it would split the workflow away from the repo-local executable.
- TUI-only improvement: rejected because it still leaves too much editing friction for the common human workflow.

## Consequences

- Pros: more ergonomic local editing, lower cognitive load than CLI-only workflows, and reused core logic.
- Pros: the executable stays repo-native and can still be distributed as a single file.
- Cons: a second interactive surface increases the amount of UI code to maintain.
- Cons: browser launch, static assets, and web hosting add packaging complexity.

## Related specs

- /specs/SPEC-WEB-LOCAL-UI.md

## Related work items

- /work/items/TASK-0023-build-local-web-ui-mode-for-workbench.md
