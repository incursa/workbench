---
artifact_id: SPEC-CLI-ITEM-UNLINK
artifact_type: specification
title: "CLI Item Unlink Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-UNLINK - CLI Item Unlink Command

## Purpose

Define the contract for removing backlinks from work items.

## Scope

- `workbench item unlink`

## REQ-CLI-ITEM-UNLINK-0001 `workbench item unlink`

`item unlink` MUST accept the work-item ID plus the documented spec, file, PR,
and issue unlink options, and remove only the selected backlinks.

## REQ-CLI-ITEM-UNLINK-0002 Selective removal

`item unlink` MUST leave unrelated backlinks intact when removing the selected
links.

## REQ-CLI-ITEM-UNLINK-0003 Dry-run behavior

`item unlink` MUST report backlink removals without writing files when
`--dry-run` is set.
