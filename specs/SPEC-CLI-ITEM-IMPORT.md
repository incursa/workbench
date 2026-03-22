---
artifact_id: SPEC-CLI-ITEM-IMPORT
artifact_type: specification
title: "CLI Item Import Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-IMPORT - CLI Item Import Command

## Purpose

Define the contract for importing GitHub issues as local work items.

## Scope

- `workbench item import`

## REQ-CLI-ITEM-IMPORT-0001 `workbench item import`

`item import` MUST accept the documented issue, type, status, priority, and
owner options, import the referenced GitHub issues into local work items, and
preserve linked issue metadata.

## REQ-CLI-ITEM-IMPORT-0002 Issue resolution

`item import` MUST accept both issue numbers and issue URLs.

## REQ-CLI-ITEM-IMPORT-0003 Invalid issue handling

`item import` MUST reject inputs that cannot be resolved to a GitHub issue.

## REQ-CLI-ITEM-IMPORT-0004 Metadata preservation

`item import` MUST carry over issue labels or references into the imported work
item metadata when those fields are available.

## REQ-CLI-ITEM-IMPORT-0005 Source provenance

`item import` MUST record the source issue number or URL in the imported work
item metadata.

## REQ-CLI-ITEM-IMPORT-0006 Duplicate import handling

`item import` MUST avoid creating a second local item for a source issue that
is already imported.

## REQ-CLI-ITEM-IMPORT-0007 Creation scope

`item import` MUST create imported work items beneath the active items
directory.
