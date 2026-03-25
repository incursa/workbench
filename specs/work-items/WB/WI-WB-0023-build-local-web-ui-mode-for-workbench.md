---
artifact_id: WI-WB-0023
artifact_type: work_item
title: "build local web ui mode for workbench"
domain: WB
status: planned
owner: platform
addresses:
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
design_links:
  - ARC-WB-0006
  - ARC-WB-0007
verification_links:
  - VER-WB-0004
  - VER-WB-0007
related_artifacts:
  - SPEC-WEB-LOCAL-UI
  - ARC-WB-0006
  - ARC-WB-0007
  - VER-WB-0004
  - VER-WB-0007
---

# WI-WB-0023 - build local web ui mode for workbench

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Add a local browser-based UI that runs from the same Workbench executable and reuses the existing core file-backed services for work items, docs, navigation sync, validation, and compact specification editing.

## Requirements Addressed

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

## Design Inputs

- [`ARC-WB-0006`](../../architecture/WB/ARC-WB-0006-local-web-ui-mode.md)
- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

## Planned Changes

- `workbench web` starts a local browser UI from the Workbench executable.
- The Specs page uses grouped identifier-family headers and compact cards that show only the spec ID and title.
- The Requirements section renders each requirement as a compact card with separate ID, title, and clause inputs.
- The Add Requirement button appends a blank requirement card to the end of the list and scrolls it into view.
- Each requirement card exposes Save, Delete, Move Earlier, and Move Later controls.
- Card Save validates and persists only the targeted card, while Save All validates every card and refuses to persist anything when one card is invalid.
- Delete removes the targeted card from the editing list, and reordering changes the list order used when the specification is saved.
- The per-card Save action rejects requirement cards that do not have an ID, title, or a clause with exactly one approved normative keyword.
- Core Narrative, Open Questions, and Related Artifacts sections stay collapsed or read-only until the user clicks Edit.
- The rendered Markdown preview starts hidden behind a collapsed toggle.
- The UI reuses the same `Workbench.Core` file-backed services instead of duplicating business logic.
- Users can browse work items, inspect details, edit a selected item, and create a new item on a separate page.
- Users can browse local docs and repo files using structured tree views and inline markdown previews.
- Users can trigger repo sync and validation actions from the UI.
- The UI works in single-file publish mode and serves its static assets from the application bundle.
- The UI exposes a local settings page for storing an authoring profile.
- Documentation and architecture decisions are linked to the work item.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

- [`VER-WB-0004`](../../verification/WB/VER-WB-0004-local-web-ui-mode.md) continues to cover the baseline local web UI shell and repo browsing behavior.
- [`VER-WB-0007`](../../verification/WB/VER-WB-0007-compact-spec-browser-and-requirement-cards.md) will cover the grouped Specs browser, compact requirement cards, per-card actions, Save All validation, section toggles, and hidden preview behavior once the UI changes land.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

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

Uses Design:

- [`ARC-WB-0006`](../../architecture/WB/ARC-WB-0006-local-web-ui-mode.md)
- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

Verified By:

- [`VER-WB-0004`](../../verification/WB/VER-WB-0004-local-web-ui-mode.md)
- [`VER-WB-0007`](../../verification/WB/VER-WB-0007-compact-spec-browser-and-requirement-cards.md)
