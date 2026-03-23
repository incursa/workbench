---
artifact_id: SPEC-CLI-CONFIG-CREDENTIALS-UNSET
artifact_type: specification
title: "CLI Config Credentials Unset Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-CONFIG
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-CONFIG-CREDENTIALS-UNSET - CLI Config Credentials Unset Command

## Purpose

Define the contract for removing a credential entry from a selected file.

## Scope

- `workbench config credentials unset`

## REQ-CLI-CONFIG-CREDENTIALS-UNSET-0001 `workbench config credentials unset`

`config credentials unset` MUST accept the documented key and path options and
remove only the named credential entry without disturbing unrelated values.

## REQ-CLI-CONFIG-CREDENTIALS-UNSET-0002 Missing key handling

`config credentials unset` MUST report a clear error when the requested key is
not present and leaves the file unchanged in that case.

## REQ-CLI-CONFIG-CREDENTIALS-UNSET-0003 Non-destructive edits

`config credentials unset` MUST preserve comments, ordering, and unrelated
entries when the credentials file format allows it.

## REQ-CLI-CONFIG-CREDENTIALS-UNSET-0004 Idempotence

`config credentials unset` MUST leave the file unchanged when the requested
key is already absent and the command is configured to ignore missing keys.

## REQ-CLI-CONFIG-CREDENTIALS-UNSET-0005 File syntax safety

`config credentials unset` MUST leave the credentials file syntactically valid
after removing an entry.

## REQ-CLI-CONFIG-CREDENTIALS-UNSET-0006 Removal summary

`config credentials unset` MUST report which credential key was removed.
