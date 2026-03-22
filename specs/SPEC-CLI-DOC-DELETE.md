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

## REQ-CLI-DOC-DELETE-0004 Cleanup ordering

`doc delete` MUST remove backlink references before deleting the document file.

## REQ-CLI-DOC-DELETE-0005 Atomic deletion

`doc delete` MUST leave the document file intact if backlink cleanup fails.

## REQ-CLI-DOC-DELETE-0006 Removed-path reporting

`doc delete` MUST report the resolved document path that was removed.

## REQ-CLI-DOC-DELETE-0007 Link retention

When `--keep-links` is set, `doc delete` MUST leave backlink references in
place.
