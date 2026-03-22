---
artifact_id: SPEC-CLI-ITEM-NEW
artifact_type: specification
title: "CLI Item New Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-NEW - CLI Item New Command

## Purpose

Define the contract for creating canonical work items.

## Scope

- `workbench item new`

## REQ-CLI-ITEM-NEW-0001 `workbench item new`

`item new` MUST accept the documented type, title, status, priority, and owner
options, allocate a new work-item ID, and create a canonical work-item file
under the active items directory.

## REQ-CLI-ITEM-NEW-0002 ID allocation

`item new` MUST allocate a fresh work-item ID from the configured sequence and
does not reuse an existing item identifier.

## REQ-CLI-ITEM-NEW-0003 File creation safety

`item new` MUST fail if the target file already exists unless the caller has
explicitly requested an overwrite path elsewhere in the command surface.
