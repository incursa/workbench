---
artifact_id: SPEC-CLI-CONFIG-SET
artifact_type: specification
title: "CLI Config Set Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-CONFIG
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-CONFIG-SET - CLI Config Set Command

## Purpose

Define the contract for mutating repo configuration values.

## Scope

- `workbench config set`

## REQ-CLI-CONFIG-SET-0001 `workbench config set`

`config set` MUST accept the documented dotted config path and value options,
support JSON parsing when requested, and persist the updated
`.workbench/config.json` atomically enough to avoid partial writes.

## REQ-CLI-CONFIG-SET-0002 Path validation

`config set` MUST reject invalid or empty config paths and leaves the
configured settings schema untouched for invalid paths.

## REQ-CLI-CONFIG-SET-0003 Value parsing

`config set` MUST treat values as strings unless `--json` is supplied and
surfaces JSON parse errors before writing any changes.

## REQ-CLI-CONFIG-SET-0004 Unrelated key preservation

`config set` MUST leave unrelated config keys unchanged.

## REQ-CLI-CONFIG-SET-0005 File syntax safety

`config set` MUST leave the configuration file syntactically valid after any
successful update.

## REQ-CLI-CONFIG-SET-0006 Dotted-path creation

`config set` MUST create missing parent objects for a dotted config path
before writing the new value.
