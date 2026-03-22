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
  - WI-WB-0024
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

`doc sync` MUST honor the `--all`, `--issues`, `--include-terminal-items`, and
`--dry-run` options as documented.

## REQ-CLI-DOC-SYNC-0003 Derived index protection

`doc sync` MUST not rewrite help or navigation indexes.

## REQ-CLI-DOC-SYNC-0004 Front-matter scope

`doc sync` MUST keep its edits limited to documentation front matter and
backlinks.

## REQ-CLI-DOC-SYNC-0005 Body preservation

`doc sync` MUST leave document bodies unchanged while normalizing metadata and
backlinks.

## REQ-CLI-DOC-SYNC-0006 Front-matter stability

`doc sync` MUST preserve existing front matter keys that it does not need to
change.

## REQ-CLI-DOC-SYNC-0007 Change reporting

`doc sync` MUST report the document paths it changed.
