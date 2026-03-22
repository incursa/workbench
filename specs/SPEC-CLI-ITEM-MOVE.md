---
artifact_id: SPEC-CLI-ITEM-MOVE
artifact_type: specification
title: "CLI Item Move Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-MOVE - CLI Item Move Command

## Purpose

Define the contract for moving work-item files.

## Scope

- `workbench item move`

## REQ-CLI-ITEM-MOVE-0001 `workbench item move`

`item move` MUST accept the work-item ID and destination path, move the file,
and update inbound links where possible.

## REQ-CLI-ITEM-MOVE-0002 Destination handling

`item move` MUST reject missing or invalid destination paths before moving the
file.

## REQ-CLI-ITEM-MOVE-0003 Link repair

`item move` MUST update inbound links that reference the old path whenever it
can do so safely.
