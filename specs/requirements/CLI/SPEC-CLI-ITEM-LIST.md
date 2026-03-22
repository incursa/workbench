---
artifact_id: SPEC-CLI-ITEM-LIST
artifact_type: specification
title: "CLI Item List Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-ITEM-LIST - CLI Item List Command

## Purpose

Define the contract for listing work items.

## Scope

- `workbench item list`

## REQ-CLI-ITEM-LIST-0001 `workbench item list`

`item list` MUST accept the documented type, status, and `--include-terminal-items`
options and list work items without mutating any files.

## REQ-CLI-ITEM-LIST-0002 Filtering behavior

`item list` MUST apply type and status filters before rendering output.

## REQ-CLI-ITEM-LIST-0003 Terminal-item visibility

`item list` MUST honor `--include-terminal-items` when deciding whether terminal items
are visible.

## REQ-CLI-ITEM-LIST-0004 Non-mutating output

`item list` MUST never write to work item files or backlinks.

## REQ-CLI-ITEM-LIST-0005 Stable ordering

`item list` MUST keep result ordering stable for the same repository state.

## REQ-CLI-ITEM-LIST-0006 Machine-readable output

`item list` MUST support machine-readable output when requested.

## REQ-CLI-ITEM-LIST-0007 Record emission

When machine-readable output is requested, `item list` MUST emit one record
per returned work item.
