---
artifact_id: WI-WB-0007
artifact_type: work_item
title: "Build TUI MVP with command preview and dry-run"
domain: WB
status: planned
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

# WI-WB-0007 - Build TUI MVP with command preview and dry-run

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Build the initial Terminal.Gui-based TUI with work item workflows, command preview,
and a global dry-run toggle for discoverability.

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

- TUI provides basic work item workflows defined in the spec (list, view, create, update).
- Bottom status line shows the last command invoked by the TUI.
- Global dry-run toggle is visible and marks outputs as dry-run.
- Errors surface CLI validation messages in dialogs.
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
