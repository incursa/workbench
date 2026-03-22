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

## REQ-CLI-ITEM-STATUS-0004 Content preservation

`item status` MUST leave the item title and body unchanged while updating the
status fields.

## REQ-CLI-ITEM-STATUS-0005 Note handling

`item status` MUST append the supplied note without rewriting unrelated body
content.

## REQ-CLI-ITEM-STATUS-0006 Status summary

`item status` MUST report the resulting status after the update completes.

## REQ-CLI-ITEM-STATUS-0007 Optional note handling

`item status` MUST omit the note section when no note is provided.
