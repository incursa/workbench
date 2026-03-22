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
  - TASK-0024
---

# SPEC-CLI-ITEM-CLOSE - CLI Item Close Command

## Purpose

Define the contract for closing work items and optionally moving them to the
done area.

## Scope

- `workbench item close`

## REQ-CLI-ITEM-CLOSE-0001 `workbench item close`

`item close` MUST accept the work-item ID and optional `--no-move` flag, set
the item status to done, and move terminal items to `work/done` unless the move
is explicitly skipped.

## REQ-CLI-ITEM-CLOSE-0002 Idempotent close behavior

`item close` MUST be safe to rerun on an already-closed item and preserves the
item's terminal status without introducing duplicate state changes.

## REQ-CLI-ITEM-CLOSE-0003 Missing item handling

`item close` MUST fail clearly when the target work item cannot be resolved.

## REQ-CLI-ITEM-CLOSE-0004 Updated-date behavior

`item close` MUST refresh the updated date when it changes the item status.

## REQ-CLI-ITEM-CLOSE-0005 Terminal-state preservation

`item close` MUST leave the item's terminal status unchanged when it is
already done.

## REQ-CLI-ITEM-CLOSE-0006 Move suppression

When `--no-move` is set, `item close` MUST update the status without moving
the file to `work/done`.

## REQ-CLI-ITEM-CLOSE-0007 Body preservation

`item close` MUST leave the work-item body unchanged while updating the
status.
