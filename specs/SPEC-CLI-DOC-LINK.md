---
artifact_id: SPEC-CLI-DOC-LINK
artifact_type: specification
title: "CLI Doc Link Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-LINK - CLI Doc Link Command

## Purpose

Define the contract for adding backlinks to documentation files.

## Scope

- `workbench doc link`

## REQ-CLI-DOC-LINK-0001 `workbench doc link`

`doc link` MUST accept the documented doc reference, doc type, and work-item
links, and add the corresponding backlinks without duplicating entries.

## REQ-CLI-DOC-LINK-0002 Duplicate suppression

`doc link` MUST ignore backlinks that already exist instead of duplicating
them.

## REQ-CLI-DOC-LINK-0003 Dry-run behavior

`doc link` MUST report backlink changes without writing files when `--dry-run`
is set.
