---
artifact_id: SPEC-CLI-DOC-UNLINK
artifact_type: specification
title: "CLI Doc Unlink Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-UNLINK - CLI Doc Unlink Command

## Purpose

Define the contract for removing selected work-item backlinks from docs.

## Scope

- `workbench doc unlink`

## REQ-CLI-DOC-UNLINK-0001 `workbench doc unlink`

`doc unlink` MUST accept the documented doc reference and work-item list,
remove the backlinks for the selected work items, and preserve unrelated links.

## REQ-CLI-DOC-UNLINK-0002 Selective removal

`doc unlink` MUST leave unrelated backlinks intact.

## REQ-CLI-DOC-UNLINK-0003 Dry-run behavior

`doc unlink` MUST report backlink removals without writing files when
`--dry-run` is set.
