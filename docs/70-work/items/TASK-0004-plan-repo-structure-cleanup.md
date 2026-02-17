---
id: TASK-0004
type: task
status: draft
priority: medium
owner: platform
created: 2025-12-27
updated: null
tags: []
related:
  specs: []
  adrs: []
  files: []
  prs: []
  issues:
    - "https://github.com/bravellian/workbench/issues/19"
  branches: []
title: "Plan repo structure cleanup (reduce large files, improve navigation)"
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/70-work/items/TASK-0004-plan-repo-structure-cleanup.md
---

# TASK-0004 - Plan repo structure cleanup (reduce large files, improve navigation)

## Summary

Create a structured plan to refactor the Workbench codebase into clearer
modules, reduce large file sizes (notably `Program.cs`), and improve overall
navigability without changing behavior.

## Goals

- Identify high-impact refactors to shrink large files and clarify ownership.
- Propose a modular layout that groups commands, services, and models.
- Define an incremental migration plan with low-risk steps.
- Specify tests or verification needed after each phase.

## Non-goals

- Implementing any refactors in this task.
- Changing CLI behavior, outputs, or schemas.

## Current pain points (observed)

- `src/Workbench/Program.cs` contains multiple command trees and helpers.
- Service classes are growing and mixing concerns.
- Output DTOs are scattered, making flow hard to trace.
- Workboard, navigation, and doc sync logic are spread across multiple files.

## Proposed plan (draft)

### Phase 1: Command layout and entrypoints

- Split `Program.cs` into command modules under `src/Workbench/Commands/`.
- Group commands by domain:
  - `Commands/Item/` (`item new`, `item sync`, `item list`, etc.)
  - `Commands/Doc/` (`doc new`, `doc sync`, `doc summarize`)
  - `Commands/Nav/` (`nav sync`)
  - `Commands/Board/` (`board regen`)
  - `Commands/Scaffold/` (`init`, `scaffold`)
  - `Commands/Repo/` (`repo sync`)
- Keep a minimal `Program.cs` that wires up command modules and shared options.

### Phase 2: Service boundaries and shared utilities

- Group services under `src/Workbench/Services/` by responsibility:
  - `Docs` (doc sync, summaries, front matter)
  - `Items` (work item CRUD, issue sync)
  - `Navigation` (indexes + readme templates)
  - `Board` (workboard generation)
  - `GitHub` (gh API, parsing, PR/issue helpers)
  - `Git` (git command wrappers)
- Create a small `Utilities/` or `Common/` namespace for shared helpers
  (string normalization, table formatting, markdown helpers).

### Phase 3: Models and outputs

- Group request/response DTOs under `src/Workbench/Outputs/`.
- Co-locate schema-related payloads with the feature domain.
- Ensure `WorkbenchJsonContext` references remain centralized.

### Phase 4: Testing and verification

- Add smoke tests (or simple CLI invocations) for:
  - `nav sync` (readme/workboard generation)
  - `item sync` (issue updates, branch behavior)
  - `doc sync` (front matter update)
- Document manual verification steps in `docs/60-tracking/ai-change-notes.md`
  or a dedicated checklist file.

### Phase 5: Documentation updates

- Update `README.md` with the new structure map.
- Add a short README in any new top-level folder (e.g., `src/Workbench/Commands/README.md`).

## Success criteria

- `Program.cs` reduced to <300 lines and contains only wiring.
- Each command has a focused module with a single responsibility.
- Services are grouped by domain with minimal cross-dependencies.
- No behavior changes (CLI output parity verified).
