---
uri: workbench://runbooks/spec-cli-workflow
slug: spec-cli-workflow
title: Spec CLI workflow
summary: How to create, inspect, link, and sync Workbench specifications.
kind: guide
group: runbooks
aliases:
  - spec-workflow
  - command-runbook
relatedUris:
  - workbench://overview
  - workbench://guides/authoring
  - workbench://reference/layout
priority: 85
includeInSearch: true
searchKind: guide
tags:
  - runbook
  - spec
  - cli
---

# Spec CLI workflow

Use the dedicated `workbench spec` command family to create, inspect, edit,
link, unlink, delete, and synchronize requirement specifications without
falling back to ad hoc Markdown editing.

## Scope

- `workbench spec new`
- `workbench spec show`
- `workbench spec edit`
- `workbench spec delete`
- `workbench spec link`
- `workbench spec unlink`
- `workbench spec sync`

## Workflow

1. Inspect the current spec or spec list.
2. Create a new spec.
3. Keep the spec body structured.
4. Link the spec to work items when delivery work starts.
5. Update or remove traceability when scope changes.
6. Sync backlinks and front matter after larger edits or file moves.
7. Use the browser UI when you want a structured editor.

## Validation

- The spec file exists under `specs`.
- `workbench validate` passes with no broken links or schema errors.
- Related work items point back to the spec file or artifact ID.
- Specs, architecture docs, work items, and verification artifacts should
  render clickable relative repository links whenever they refer to another
  local document.
