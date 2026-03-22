---
artifact_id: SPEC-CLI-SPEC-SYNC
artifact_type: specification
title: "CLI Spec Sync Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SPEC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-SPEC-SYNC - CLI Spec Sync Command

## Purpose

Define the contract for synchronizing spec front matter and backlinks.

## Scope

- `workbench spec sync`

## REQ-CLI-SPEC-SYNC-0001 `workbench spec sync`

`spec sync` MUST accept the documented all/issues/include-done/dry-run options,
normalize spec front matter and backlinks, and not regenerate derived indexes.

## REQ-CLI-SPEC-SYNC-0002 Scope selection

`spec sync` MUST honor the requested sync scope and leaves unrelated docs
untouched.

## REQ-CLI-SPEC-SYNC-0003 Dry-run behavior

`spec sync` MUST report planned changes without writing files when `--dry-run`
is set.
