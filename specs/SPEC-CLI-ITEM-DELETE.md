---
artifact_id: SPEC-CLI-ITEM-DELETE
artifact_type: specification
title: "CLI Item Delete Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-DELETE - CLI Item Delete Command

## Purpose

Define the contract for deleting work items.

## Scope

- `workbench item delete`

## REQ-CLI-ITEM-DELETE-0001 `workbench item delete`

`item delete` MUST accept the work-item ID and `--keep-links` option, delete
the work item file, and update doc backlinks unless link removal is skipped.

## REQ-CLI-ITEM-DELETE-0002 Link cleanup

`item delete` MUST remove backlinks to the deleted item from docs unless
`--keep-links` is set.

## REQ-CLI-ITEM-DELETE-0003 Missing target handling

`item delete` MUST fail clearly when the referenced item file does not exist.

## REQ-CLI-ITEM-DELETE-0004 Cleanup ordering

`item delete` MUST remove backlink references before deleting the item file.

## REQ-CLI-ITEM-DELETE-0005 Atomic deletion

`item delete` MUST leave the item file intact if backlink cleanup fails.

## REQ-CLI-ITEM-DELETE-0006 Removed-path reporting

`item delete` MUST report the resolved item path that was removed.

## REQ-CLI-ITEM-DELETE-0007 Link retention

When `--keep-links` is set, `item delete` MUST leave backlink references in
place.
