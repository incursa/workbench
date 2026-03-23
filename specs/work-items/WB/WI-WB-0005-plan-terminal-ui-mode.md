---
artifact_id: WI-WB-0005
artifact_type: work_item
title: "Plan terminal UI mode"
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

# WI-WB-0005 - Plan terminal UI mode

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Plan the embedded terminal UI mode for Workbench to improve command discoverability,
with a visible command preview and a global dry-run toggle.

## Requirements Addressed

- REQ-TUI-0001
- REQ-TUI-0002
- REQ-TUI-0003
- REQ-TUI-0004
- REQ-TUI-0005

## Design Inputs

- ARC-WB-0003
- ARC-WB-0007

## Planned Changes

- Spec defines TUI scope, workflows, and UX requirements including command preview and dry-run mode.
- Architecture artifact records the decision to embed TUI as a subcommand with a shared core and single executable.
- Work item links to the spec and architecture artifact.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- REQ-TUI-0001
- REQ-TUI-0002
- REQ-TUI-0003
- REQ-TUI-0004
- REQ-TUI-0005

Uses Design:

- ARC-WB-0003
- ARC-WB-0007

Verified By:

- VER-WB-0003
