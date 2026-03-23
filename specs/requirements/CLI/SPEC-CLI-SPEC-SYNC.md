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
  - WI-WB-0024
---

# SPEC-CLI-SPEC-SYNC - CLI Spec Sync Command

## Purpose

Define the contract for synchronizing spec front matter and backlinks.

## Scope

- `workbench spec sync`

## REQ-CLI-SPEC-SYNC-0001 `workbench spec sync`

`spec sync` MUST accept the documented all/issues/include-terminal-items/dry-run options,
normalize spec front matter and backlinks, and not regenerate derived indexes.

## REQ-CLI-SPEC-SYNC-0002 Scope selection

`spec sync` MUST honor the requested sync scope and leaves unrelated docs
untouched.

## REQ-CLI-SPEC-SYNC-0003 Dry-run behavior

`spec sync` MUST report planned changes without writing files when `--dry-run`
is set.

## REQ-CLI-SPEC-SYNC-0004 Front-matter scope

`spec sync` MUST keep its edits limited to spec front matter and backlinks.

## REQ-CLI-SPEC-SYNC-0005 Body preservation

`spec sync` MUST leave specification bodies unchanged while normalizing
metadata and backlinks.

## REQ-CLI-SPEC-SYNC-0006 Front-matter stability

`spec sync` MUST preserve existing front matter keys that it does not need to
change.
