---
artifact_id: SPEC-CLI-DOC-SYNC
artifact_type: specification
title: "CLI Doc Sync Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-SYNC - CLI Doc Sync Command

## Purpose

Define the contract for normalizing documentation front matter and backlinks.

## Scope

- `workbench doc sync`

## REQ-CLI-DOC-SYNC-0001 `workbench doc sync`

`doc sync` MUST accept the documented sync options, normalize doc front matter
and backlinks, and not regenerate derived indexes.

## REQ-CLI-DOC-SYNC-0002 Scope selection

`doc sync` MUST honor the `--all`, `--issues`, `--include-done`, and
`--dry-run` options as documented.

## REQ-CLI-DOC-SYNC-0003 Derived index protection

`doc sync` MUST not rewrite help or navigation indexes.
