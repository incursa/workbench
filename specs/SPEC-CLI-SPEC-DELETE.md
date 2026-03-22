---
artifact_id: SPEC-CLI-SPEC-DELETE
artifact_type: specification
title: "CLI Spec Delete Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SPEC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-SPEC-DELETE - CLI Spec Delete Command

## Purpose

Define the contract for deleting specification files.

## Scope

- `workbench spec delete`

## REQ-CLI-SPEC-DELETE-0001 `workbench spec delete`

`spec delete` MUST accept the spec path/link/artifact reference and
`--keep-links` option, delete the specification file, and update work-item
links unless link removal is skipped.

## REQ-CLI-SPEC-DELETE-0002 Target resolution

`spec delete` MUST resolve a spec by artifact ID, path, or link before
deleting it.

## REQ-CLI-SPEC-DELETE-0003 Missing target handling

`spec delete` MUST fail clearly when the referenced spec cannot be found.
