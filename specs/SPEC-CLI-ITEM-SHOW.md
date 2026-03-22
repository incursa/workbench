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

## REQ-CLI-ITEM-SHOW-0004 Missing-target handling

`item show` MUST fail clearly when the referenced work item cannot be resolved.

## REQ-CLI-ITEM-SHOW-0005 Canonical path output

`item show` MUST print the resolved canonical path alongside the work-item ID.

## REQ-CLI-ITEM-SHOW-0006 Metadata completeness

`item show` MUST render the resolved work-item metadata before the body or
summary content.

## REQ-CLI-ITEM-SHOW-0007 Stable field order

`item show` MUST render metadata fields in a stable order.
