---
artifact_id: SPEC-WEB-LOCAL-UI
artifact_type: specification
title: Local Web UI Mode
domain: WEB
capability: local-ui
status: draft
owner: platform
related_artifacts:
  - WI-WB-0023
  - ARC-WB-0006
---

# SPEC-WEB-LOCAL-UI - Local Web UI Mode

## Purpose

Add a browser-based local UI to Workbench that runs inside the same executable
and reuses the existing core file-backed services for local repo management.

## Scope

- provide a click-first local editing experience for work items
- add a compact repo overview page
- add docs and file browsers with tree-style grouping
- add a separate create-item page
- keep the UI repo-native and single-file publish friendly

## Context

The CLI and TUI are strong for automation and keyboard-driven workflows, but a
browser-based local surface helps when browsing many files, rendering markdown,
or editing structured fields with a pointer-first workflow. The UI should not
replace the CLI; it should reuse the same core services so behavior stays
consistent.

## REQ-WEB-0001 Run from the same executable
The UI MUST run from the same published Workbench executable that provides the CLI.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- keep the host single-file friendly

## REQ-WEB-0002 Reuse shared core services
The UI MUST use the existing Workbench.Core services for item creation, editing, status changes, sync, and validation.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- avoid duplicating parsing or mutation logic in the UI layer

## REQ-WEB-0003 Support local browsing and editing
The UI MUST provide a browsable work-item view, a selected-item editor, a separate create page, and repo-local doc and file browsing with rendered markdown previews.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- keep docs browsing tree-oriented instead of flat
- keep item creation separate from editing

## REQ-WEB-0004 Support local management without GitHub
The UI MUST support local work-item and doc management without requiring GitHub connectivity.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- keep GitHub issues as the coordination surface, not the canonical store

## REQ-WEB-0005 Expose obvious interactive entry points
The UI MUST include an obvious browser-launch path for interactive sessions and support a machine-local author profile for default owner and name values.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- keep the author profile machine-local
- ensure static assets ship with the executable

## Open Questions

- Which pages should stay CLI-only for now?
- Should the web UI expose raw markdown editing, or keep editing constrained to structured fields?
- Should browser launch be automatic by default or opt-in?
