---
artifact_id: SPEC-CLI-SCAFFOLD
artifact_type: specification
title: "CLI Scaffold Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-OPERATIONS
  - WI-WB-0024
---

# SPEC-CLI-SCAFFOLD - CLI Scaffold Command

## Purpose

Define the contract for creating the standard repository layout.

## Scope

- `workbench scaffold`

## REQ-CLI-SCAFFOLD-0001 `workbench scaffold`

`scaffold` MUST accept the documented force option, create the standard
repository layout, and never silently overwrite existing files without explicit
permission.

## REQ-CLI-SCAFFOLD-0002 Existing file protection

`scaffold` MUST preserve existing authored content unless `--force` is
provided and, when it replaces files, only replaces files that the command
explicitly owns.

## REQ-CLI-SCAFFOLD-0003 Layout determinism

`scaffold` MUST create the same directory and template structure for the same
repository state so bootstrap runs are repeatable.

## REQ-CLI-SCAFFOLD-0004 Ownership boundary

`scaffold` MUST only create files that belong to the standard scaffold
structure unless `--force` is requested.

## REQ-CLI-SCAFFOLD-0005 Missing-tree creation

`scaffold` MUST create the default folder and template structure when those
paths are absent.
