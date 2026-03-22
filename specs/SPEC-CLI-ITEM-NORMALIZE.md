---
artifact_id: SPEC-CLI-ITEM-NORMALIZE
artifact_type: specification
title: "CLI Item Normalize Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-NORMALIZE - CLI Item Normalize Command

## Purpose

Define the contract for normalizing work-item front matter.

## Scope

- `workbench item normalize`

## REQ-CLI-ITEM-NORMALIZE-0001 `workbench item normalize`

`item normalize` MUST accept the documented include-done and dry-run options,
normalize work-item front matter lists, and leave files untouched in dry-run
mode.

## REQ-CLI-ITEM-NORMALIZE-0002 Scope selection

`item normalize` MUST only normalize the selected item set.

## REQ-CLI-ITEM-NORMALIZE-0003 Non-target preservation

`item normalize` MUST preserve all non-target files.

## REQ-CLI-ITEM-NORMALIZE-0004 Ordering and deduplication

`item normalize` MUST deduplicate and canonicalize list-valued front matter in a
stable order.
