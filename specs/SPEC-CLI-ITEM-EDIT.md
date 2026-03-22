---
artifact_id: SPEC-CLI-ITEM-EDIT
artifact_type: specification
title: "CLI Item Edit Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-EDIT - CLI Item Edit Command

## Purpose

Define the contract for editing work-item content.

## Scope

- `workbench item edit`

## REQ-CLI-ITEM-EDIT-0001 `workbench item edit`

`item edit` MUST accept the documented title, summary, acceptance, note, and
path-preservation options, update the managed sections coherently, and keep the
slug/title/body alignment consistent.

## REQ-CLI-ITEM-EDIT-0002 Body section integrity

`item edit` MUST only rewrite the Summary, Acceptance criteria, and Notes
sections that the selected flags target.

## REQ-CLI-ITEM-EDIT-0003 Body preservation

`item edit` MUST preserve unrelated body content.

## REQ-CLI-ITEM-EDIT-0004 Title and slug behavior

`item edit` MUST rename the file and regenerate the slug when `--keep-path` is
not set.

## REQ-CLI-ITEM-EDIT-0005 Keep-path behavior

`item edit` MUST leave the file path unchanged when `--keep-path` is set.
