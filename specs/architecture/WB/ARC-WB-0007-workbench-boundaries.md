---
artifact_id: ARC-WB-0007
artifact_type: architecture
title: "Workbench boundaries"
domain: WB
status: draft
owner: platform
satisfies:
  - REQ-WB-STD-0001
  - REQ-WB-STD-0002
  - REQ-WB-STD-0003
  - REQ-WB-STD-0004
  - REQ-CLI-SURFACE-0001
  - REQ-CLI-SURFACE-0002
  - REQ-CLI-SURFACE-0003
  - REQ-CLI-SURFACE-0004
  - REQ-CLI-SURFACE-0005
  - REQ-CLI-SURFACE-0006
  - REQ-TUI-0001
  - REQ-TUI-0002
  - REQ-TUI-0003
  - REQ-TUI-0004
  - REQ-TUI-0005
  - REQ-WEB-0001
  - REQ-WEB-0002
  - REQ-WEB-0003
  - REQ-WEB-0004
  - REQ-WEB-0005
related_artifacts:
  - SPEC-WB-STD
  - SPEC-CLI-SURFACE
  - SPEC-TUI-TERMINAL-UI
  - SPEC-WEB-LOCAL-UI
  - VER-WB-0005
---

# ARC-WB-0007 - Workbench boundaries

## Purpose

Describe the top-level product boundaries that Workbench owns.

## Requirements Satisfied

- [`REQ-WB-STD-0001`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0002`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0003`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-WB-STD-0004`](../../requirements/WB/SPEC-WB-STD.md)
- [`REQ-CLI-SURFACE-0001`](../../requirements/CLI/SPEC-CLI-SURFACE.md)
- [`REQ-CLI-SURFACE-0002`](../../requirements/CLI/SPEC-CLI-SURFACE.md)
- [`REQ-CLI-SURFACE-0003`](../../requirements/CLI/SPEC-CLI-SURFACE.md)
- [`REQ-CLI-SURFACE-0004`](../../requirements/CLI/SPEC-CLI-SURFACE.md)
- [`REQ-CLI-SURFACE-0005`](../../requirements/CLI/SPEC-CLI-SURFACE.md)
- [`REQ-CLI-SURFACE-0006`](../../requirements/CLI/SPEC-CLI-SURFACE.md)
- [`REQ-TUI-0001`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0002`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0003`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0004`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-TUI-0005`](../../requirements/TUI/SPEC-TUI-TERMINAL-UI.md)
- [`REQ-WEB-0001`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0002`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0003`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0004`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0005`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)

## Design Summary

Workbench is a single product with multiple surfaces:

- the standards integration layer defines how Workbench reads, writes, and
  validates spec-trace artifacts
- the CLI defines the automation-first command model
- the web UI defines the browser-first interactive model
- shared services own parsing, mutation, validation, and repo discovery

## Key Components

- Standards integration layer
- CLI automation surface
- Web interactive surface
- Shared parsing, mutation, validation, and discovery services

## Data and State Considerations

The boundary model separates authored content from generated views and keeps each interactive surface thin over the shared core.

## Edge Cases and Constraints

- Canonical artifacts should remain distinguishable from generated indexes.
- The CLI, TUI, and web surfaces should reuse the same repository model.
- Generated sections should be clearly marked as generated.

## Alternatives Considered

- <alternative and reason rejected>

## Risks

- <risk or follow-up>

## Open Questions

- None.
