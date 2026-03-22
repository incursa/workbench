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
  - WI-WB-0024
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

## REQ-CLI-ITEM-RENAME-0004 Identity preservation

`item rename` MUST keep the work-item ID unchanged while renaming the file and
slug.

## REQ-CLI-ITEM-RENAME-0005 Title validation

`item rename` MUST reject empty or whitespace-only new titles before renaming
the file.

## REQ-CLI-ITEM-RENAME-0006 Missing-item handling

`item rename` MUST fail clearly when the referenced work item cannot be
resolved.

## REQ-CLI-ITEM-RENAME-0007 Path reporting

`item rename` MUST report the resolved renamed file path.
