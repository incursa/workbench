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
  - VER-WB-0007
---

# SPEC-WEB-LOCAL-UI - Local Web UI Mode

## Purpose

Add a browser-based local UI to Workbench that runs inside the same executable
and reuses the existing core file-backed services for local repo management and
structured specification editing.

## Scope

- provide a click-first local editing experience for work items
- add a compact specs browser with stable identifier-family grouping
- add card-based requirement editing with save-time validation
- add docs and file browsers with tree-style grouping
- add a separate create-item page
- keep the UI repo-native and single-file publish friendly

## Context

The CLI and TUI are strong for automation and keyboard-driven workflows, but a
browser-based local surface helps when browsing many files, rendering markdown,
or editing structured fields with a pointer-first workflow. The local web UI
should not replace the CLI; it should reuse the same core services so behavior
stays consistent.

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

## REQ-WEB-0006 Keep cards padded and separated
The UI MUST keep page content and cards visually separated with interior padding and spacing so controls do not touch the viewport edge or card borders.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- use the same spacing treatment across the specs page

## REQ-WEB-0007 Group specs by identifier family
The Specs page MUST group specification cards under stable identifier-family headers derived from the `SPEC-<DOMAIN>` prefix of the artifact ID instead of repository-path tree levels.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- examples include `SPEC-CLI`, `SPEC-QA`, and `SPEC-WEB`

## REQ-WEB-0008 Render compact spec cards
Each specification card MUST display only the specification artifact ID and title in the default list view.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- the file path and summary excerpt stay hidden from the default view

## REQ-WEB-0009 Remove nested browser tree levels
The Specs page MUST NOT require nested collapsible repository, spec, or requirement tree sections for navigating the specification list.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- group headers may remain collapsible

## REQ-WEB-0010 Add requirement cards
The Requirements section MUST include an Add Requirement button that appends a blank requirement card to the end of the list and scrolls it into view.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- the new card should start with empty requirement fields

## REQ-WEB-0011 Expose requirement fields explicitly
The requirement editor MUST expose separate inputs for requirement ID, title, and clause.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- the ID input controls the `REQ-...` heading token
- the title input controls the heading title
- the clause input controls the sentence that appears immediately after the heading

## REQ-WEB-0012 Validate requirement clauses on save
The page Save button MUST reject any requirement whose ID is missing or malformed, whose title is missing, or whose clause does not contain exactly one approved normative keyword.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- approved keywords are `MUST`, `MUST NOT`, `SHALL`, `SHALL NOT`, `SHOULD`, `SHOULD NOT`, and `MAY`

## REQ-WEB-0013 Collapse optional sections until edited
The Core Narrative, Open Questions, and Related Artifacts sections MUST render as read-only or collapsed by default when empty.

Trace:
- Implemented By:
  - [WI-WB-0023](/specs/work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
- Related:
  - [ARC-WB-0006](/architecture/ARC-WB-0006-local-web-ui-mode.md)

Notes:
- their Edit button expands the section into editable inputs without discarding saved values

## Open Questions

- Should the Specs page keep the rendered Markdown preview, hide it behind a toggle, or remove it?
- Should requirement cards support delete and reordering actions in the first pass, or only append and edit?
- Should identifier-family grouping always use the `SPEC-<DOMAIN>` prefix, or should it follow a configurable registry when one exists?
