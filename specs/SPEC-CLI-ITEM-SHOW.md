---
artifact_id: SPEC-CLI-ITEM-SHOW
artifact_type: specification
title: "CLI Item Show Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-SHOW - CLI Item Show Command

## Purpose

Define the contract for rendering a work item's resolved metadata.

## Scope

- `workbench item show`

## REQ-CLI-ITEM-SHOW-0001 `workbench item show`

`item show` MUST accept a work-item ID and render the resolved metadata and
path for that item.

## REQ-CLI-ITEM-SHOW-0002 Resolution fidelity

`item show` MUST resolve the canonical file path before rendering the item so
the output matches the file that would be edited or linked.

## REQ-CLI-ITEM-SHOW-0003 Read-only behavior

`item show` MUST not mutate the work item file or its backlinks.
