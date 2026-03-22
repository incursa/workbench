---
artifact_id: SPEC-CLI-DOC-DELETE
artifact_type: specification
title: "CLI Doc Delete Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-DELETE - CLI Doc Delete Command

## Purpose

Define the contract for deleting documentation files.

## Scope

- `workbench doc delete`

## REQ-CLI-DOC-DELETE-0001 `workbench doc delete`

`doc delete` MUST accept the documented doc reference and `--keep-links`
option, delete the targeted documentation file, and update work-item backlinks
unless link removal is explicitly skipped.

## REQ-CLI-DOC-DELETE-0002 Target resolution

`doc delete` MUST resolve either an artifact ID or a repo path before deleting
the target document.

## REQ-CLI-DOC-DELETE-0003 Missing target handling

`doc delete` MUST fail clearly when the referenced document cannot be found.
