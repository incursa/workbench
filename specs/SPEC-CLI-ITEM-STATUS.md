---
artifact_id: SPEC-CLI-ITEM-STATUS
artifact_type: specification
title: "CLI Item Status Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-STATUS - CLI Item Status Command

## Purpose

Define the contract for changing work-item status.

## Scope

- `workbench item status`

## REQ-CLI-ITEM-STATUS-0001 `workbench item status`

`item status` MUST accept the work-item ID, the new status, and the optional
note, update the status and updated date, and append the note when provided.

## REQ-CLI-ITEM-STATUS-0002 Status validation

`item status` MUST reject unsupported status values before writing changes.

## REQ-CLI-ITEM-STATUS-0003 Updated-date behavior

`item status` MUST refresh the updated date whenever it changes the status.
