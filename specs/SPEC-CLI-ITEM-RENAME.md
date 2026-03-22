---
artifact_id: SPEC-CLI-ITEM-RENAME
artifact_type: specification
title: "CLI Item Rename Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-RENAME - CLI Item Rename Command

## Purpose

Define the contract for renaming work items.

## Scope

- `workbench item rename`

## REQ-CLI-ITEM-RENAME-0001 `workbench item rename`

`item rename` MUST accept the work-item ID and new title, regenerate the slug,
rename the file when required, and update inbound links.

## REQ-CLI-ITEM-RENAME-0002 Title and slug coherence

`item rename` MUST keep the heading, front matter title, and file slug aligned
after the rename completes.

## REQ-CLI-ITEM-RENAME-0003 Link update safety

`item rename` MUST repair inbound links to the renamed file when it can do so
without introducing duplicates.
