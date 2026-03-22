---
artifact_id: SPEC-CLI-SYNC
artifact_type: specification
title: "CLI Sync Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-OPERATIONS
  - TASK-0024
---

# SPEC-CLI-SYNC - CLI Sync Command

## Purpose

Define the contract for umbrella repository synchronization.

## Scope

- `workbench sync`

## REQ-CLI-SYNC-0001 `workbench sync`

`sync` MUST act as the umbrella repo sync command, accept the documented stage
options, and run the selected item/doc/nav stages in a way that preserves the
non-destructive sync model.

## REQ-CLI-SYNC-0002 Stage composition

`sync` MUST allow the item, doc, and nav stages to be selected independently
while preserving their documented stage-specific contracts.

## REQ-CLI-SYNC-0003 Dry-run behavior

`sync` MUST report the combined stage changes without writing files when
`--dry-run` is set.
