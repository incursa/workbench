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
  - REQ-WEB-0006
  - REQ-WEB-0007
  - REQ-WEB-0008
  - REQ-WEB-0009
  - REQ-WEB-0010
  - REQ-WEB-0011
  - REQ-WEB-0012
  - REQ-WEB-0013
  - REQ-WEB-0014
  - REQ-WEB-0015
  - REQ-WEB-0016
related_artifacts:
  - SPEC-WEB-LOCAL-UI
  - WI-WB-0023
  - VER-WB-0004
  - VER-WB-0007
---

# ARC-WB-0006 - Local web UI mode in the Workbench executable

## Purpose

Workbench already has a CLI and a TUI, but the most ergonomic human editing experience is often a browser-based form and list workflow. GitHub issues are convenient for PM-style triage, but the authoritative repo-local state lives in markdown files and front matter. We need a local UI that feels easier than memorizing commands while still reusing the same file-backed logic and remaining single-file friendly.

## Requirements Satisfied

- [`REQ-WEB-0001`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0002`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0003`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0004`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0005`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0006`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0007`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0008`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0009`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0010`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0011`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0012`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0013`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0014`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0015`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0016`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)

## Design Summary

Add a browser-based local UI mode to the existing Workbench executable using ASP.NET Core Razor Pages. The web UI runs as another mode of the same binary, uses the existing `Workbench.Core` services for reads and writes, and ships with embedded static assets so the published tool can remain a single-file artifact. The first pass includes a compact overview page, a grouped Specs browser with compact identifier-only cards, a requirement editor with explicit ID/title/clause fields, per-card Save/Delete/Move controls, all-or-nothing Save All validation, a separate create page, docs browser with rendered markdown and tree grouping, a repo file explorer, a hidden-by-default markdown preview toggle, and a machine-local author profile page.

## Key Components

- Browser-based local editing inside the same executable
- Shared `Workbench.Core` services for reads and writes
- Grouped Specs browser with compact cards
- Requirement cards with explicit field-level editing and per-card actions
- Save-time validation for requirement identifiers and clauses, plus batch validation for Save All
- Repo browsing with markdown rendering and tree grouping
- Collapsed optional narrative sections and a hidden-by-default preview toggle
- Machine-local author profile and static assets

## Data and State Considerations

The web UI should use the same file-backed repository state as the CLI and remain single-file friendly for distribution. Requirement card edits should preserve source order unless the user reorders cards. A failed Save All must not partially persist any cards, and invalid cards should remain visibly flagged until corrected or saved individually. Empty narrative sections should preserve saved values when they are collapsed and reopened.

## Edge Cases and Constraints

- GitHub connectivity must not be required for local editing.
- Browser launch should be obvious and repeatable.
- The UI should preserve the repo-native store rather than introducing a separate backend.
- The Specs browser should not depend on repository-path tree nesting.
- Requirement validation should fail before a malformed specification is written.
- Optional sections should not lose content when they are toggled between collapsed and editable states.
- The rendered preview should remain hidden by default and should not dominate the editing layout.

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

- Should identifier-family grouping always use the stable `SPEC-<DOMAIN>` prefix, or should the UI read grouping rules from a registry when one exists?
