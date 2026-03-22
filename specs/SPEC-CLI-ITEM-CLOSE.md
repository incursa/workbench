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
