---
artifact_id: ARC-WB-0006
artifact_type: architecture
title: "Local web UI mode in the Workbench executable"
domain: WB
status: proposed
owner: platform
satisfies:
  - REQ-WEB-0001
  - REQ-WEB-0002
  - REQ-WEB-0003
  - REQ-WEB-0004
  - REQ-WEB-0005
related_artifacts:
  - SPEC-WEB-LOCAL-UI
  - WI-WB-0023
  - VER-WB-0004
---

# ARC-WB-0006 - Local web UI mode in the Workbench executable

## Purpose

Workbench already has a CLI and a TUI, but the most ergonomic human editing experience is often a browser-based form and list workflow. GitHub issues are convenient for PM-style triage, but the authoritative repo-local state lives in markdown files and front matter. We need a local UI that feels easier than memorizing commands while still reusing the same file-backed logic and remaining single-file friendly.

## Requirements Satisfied

- REQ-WEB-0001
- REQ-WEB-0002
- REQ-WEB-0003
- REQ-WEB-0004
- REQ-WEB-0005

## Design Summary

Add a browser-based local UI mode to the existing Workbench executable using ASP.NET Core Razor Pages. The web UI will run as another mode of the same binary, use the existing `Workbench.Core` services for reads and writes, and ship with embedded static assets so the published tool can remain a single-file artifact. The first pass includes a compact overview page, work-item browser/editor, a separate create page, docs browser with rendered markdown and tree grouping, a repo file explorer, and a machine-local author profile page.

## Key Components

- Browser-based local editing inside the same executable
- Shared `Workbench.Core` services for reads and writes
- Repo browsing with markdown rendering and tree grouping
- Machine-local author profile and static assets

## Data and State Considerations

The web UI should use the same file-backed repository state as the CLI and remain single-file friendly for distribution.

## Edge Cases and Constraints

- GitHub connectivity must not be required for local editing.
- Browser launch should be obvious and repeatable.
- The UI should preserve the repo-native store rather than introducing a separate backend.

## Alternatives Considered

- VS Code extension: rejected as the primary surface because it requires a specific editor and marketplace distribution.
- Electron desktop app: rejected for now because it would add a larger packaging and maintenance burden than the UI problem warrants.
- Separate web service: rejected because it would split the workflow away from the repo-local executable.
- TUI-only improvement: rejected because it still leaves too much editing friction for the common human workflow.

## Risks

- Pros: more ergonomic local editing, lower cognitive load than CLI-only workflows, and reused core logic.
- Pros: the executable stays repo-native and can still be distributed as a single file.
- Cons: a second interactive surface increases the amount of UI code to maintain.
- Cons: browser launch, static assets, and web hosting add packaging complexity.

## Open Questions

- None.
