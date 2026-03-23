---
artifact_id: SPEC-CLI-ITEM-CLOSE
artifact_type: specification
title: "CLI Item Close Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-ITEM-CLOSE - CLI Item Close Command

## Purpose

Define the contract for closing work items by updating their canonical status
without moving them between folders.

## Scope

- `workbench item close`

## REQ-CLI-ITEM-CLOSE-0001 `workbench item close`

`item close` MUST accept the work-item ID and set the item status to
`complete` without changing the file location.

## REQ-CLI-ITEM-CLOSE-0002 Idempotent close behavior

`item close` MUST be safe to rerun on an already-complete item and preserve the
item's terminal status without introducing duplicate state changes.

## REQ-CLI-ITEM-CLOSE-0003 Missing item handling

`item close` MUST fail clearly when the target work item cannot be resolved.

## REQ-CLI-ITEM-CLOSE-0004 Updated-date behavior

`item close` MUST refresh the updated date when it changes the item status.

## REQ-CLI-ITEM-CLOSE-0005 Terminal-state preservation

`item close` MUST leave the item's terminal status unchanged when it is
already complete.

## REQ-CLI-ITEM-CLOSE-0007 Body preservation

`item close` MUST leave the work-item body unchanged while updating the
status.
