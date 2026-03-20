---
workbench:
  type: spec
  workItems:
    - TASK-0023
  codeRefs: []
  pathHistory:
    - "C:/docs/10-product/feature-spec-local-web-ui.md"
  path: /docs/10-product/feature-spec-local-web-ui.md
owner: platform
status: draft
updated: 2026-03-20
---

# Feature Spec: Local Web UI Mode

## Summary

Add a browser-based local UI to Workbench that runs inside the same executable and reuses the existing core file-backed services. The UI is intended for human-friendly browsing and editing of local work items, docs, and repo files while preserving the CLI as the power-user interface.

## Goals

- Provide a click-first local editing experience for work items.
- Add a compact repo overview page, but keep the main landing experience centered on work items.
- Add docs and file browsers with tree-style grouping so repo-local discovery is structured instead of flat.
- Add a separate create-item page so creating and editing do not share the same screen.
- Keep the executable single-file friendly and repo-native.
- Reuse the same Workbench.Core parsing, validation, sync, and mutation code.
- Keep GitHub issues as the coordination surface, not the canonical store.

## Non-goals

- Replacing the CLI or TUI.
- Building a full standalone desktop app.
- Introducing a database or separate backend store.
- Making the local UI and GitHub issues symmetrical.

## User stories / scenarios

- As a user, I can open `workbench web` and browse active work items in a browser.
- As a user, I can click a work item, edit its core fields, and save changes locally.
- As a user, I can create a new work item on a separate page instead of typing a command.
- As a user, I can trigger sync and validation actions from the UI.
- As a user, I can inspect related docs, render markdown previews, and browse repo files in a structured tree.

## Requirements

- The UI must run from the same published Workbench executable.
- The UI must use the existing Workbench.Core file-backed services for item creation, editing, status changes, sync, and validation.
- The UI must discover the repo root automatically when launched from a repository checkout.
- The UI must work without GitHub connectivity for purely local item management.
- The UI must be server-rendered and lightweight enough to run as a local repo tool.
- The UI must include an obvious browser-launch path for interactive sessions.
- Static assets must ship with the executable in single-file publish mode.
- The UI must support a machine-local author profile for default owner/name values.
- The UI must render markdown docs inline rather than showing only raw source text.
- The UI must include a repo file explorer for discovering and previewing local files.
- The UI must keep item creation separate from the selected-item editor.

## UX notes

- Prefer a compact layout with a left item list and a right detail/editor pane.
- Keep the primary actions visible: edit, sync, validate, and navigate.
- Make the item list filterable by status and include-done state.
- Use tree-style grouping in docs and file browsers so the structure stays visible.
- Provide a separate create page and a compact overview page without forcing the user to leave the app.
- Keep copy terse and repo-native, matching the CLI terminology.

## Dependencies

- ASP.NET Core Razor Pages.
- Existing Workbench.Core services for work item, doc, navigation, and validation operations.
- Embedded static file support for single-file publish mode.
- A machine-local profile file for authoring defaults.

## Risks and mitigations

- Risk: web UI drifts from CLI behavior.
  - Mitigation: call the same core service methods and keep the UI thin.
- Risk: browser UI becomes another partially-maintained surface.
  - Mitigation: keep the first version narrow and focused on the highest-friction workflows.
- Risk: embedded assets and single-file hosting add packaging complexity.
  - Mitigation: mirror the working single-file host pattern used in the reference app.

## Related work items

- /docs/70-work/items/TASK-0023-build-local-web-ui-mode-for-workbench.md

## Related ADRs

- /docs/40-decisions/ADR-2026-03-20-local-web-ui-mode.md

## Open questions

- Which pages should stay CLI-only for now?
- Should the web UI expose raw markdown editing, or keep editing constrained to structured fields?
- Should browser launch be automatic by default or opt-in?
