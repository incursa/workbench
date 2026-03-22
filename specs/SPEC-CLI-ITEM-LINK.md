---
artifact_id: SPEC-CLI-ITEM-LINK
artifact_type: specification
title: "CLI Item Link Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-LINK - CLI Item Link Command

## Purpose

Define the contract for adding backlinks to work items.

## Scope

- `workbench item link`

## REQ-CLI-ITEM-LINK-0001 `workbench item link`

`item link` MUST accept the work-item ID plus the documented spec, file, PR,
and issue link options, and add the selected backlinks without duplicating
existing entries.

## REQ-CLI-ITEM-LINK-0002 Duplicate suppression

`item link` MUST leave existing backlinks unchanged when the requested link is
already present.

## REQ-CLI-ITEM-LINK-0003 Dry-run behavior

`item link` MUST report the backlink changes without writing files when
`--dry-run` is set.
