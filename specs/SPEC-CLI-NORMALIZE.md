---
artifact_id: SPEC-CLI-NORMALIZE
artifact_type: specification
title: "CLI Normalize Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-OPERATIONS
  - TASK-0024
---

# SPEC-CLI-NORMALIZE - CLI Normalize Command

## Purpose

Define the contract for repository normalization actions.

## Scope

- `workbench normalize`

## REQ-CLI-NORMALIZE-0001 `workbench normalize`

`normalize` MUST accept the documented items/docs/include-done/all-docs/dry-run
options and normalize the selected front matter without making unrelated edits.

## REQ-CLI-NORMALIZE-0002 Selection behavior

`normalize` MUST only touch the document classes selected by `--items`,
`--docs`, and `--all-docs`, and does not infer additional targets.

## REQ-CLI-NORMALIZE-0003 Dry-run behavior

`normalize` MUST report all planned changes without writing files when
`--dry-run` is set.
