---
artifact_id: SPEC-CLI-CONFIG-CREDENTIALS-SET
artifact_type: specification
title: "CLI Config Credentials Set Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-CONFIG
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-CONFIG-CREDENTIALS-SET - CLI Config Credentials Set Command

## Purpose

Define the contract for writing credentials to a selected credentials file.

## Scope

- `workbench config credentials set`

## REQ-CLI-CONFIG-CREDENTIALS-SET-0001 `workbench config credentials set`

`config credentials set` MUST accept the documented key, value, and path
options, write or update the selected credentials file, and ensure repo-local
credential files are ignored by git.

## REQ-CLI-CONFIG-CREDENTIALS-SET-0002 File creation and persistence

`config credentials set` MUST create the credentials file and parent directory
when they do not exist and preserves existing credential entries that are not
being updated.

## REQ-CLI-CONFIG-CREDENTIALS-SET-0003 Write safety

`config credentials set` MUST write the credentials file atomically enough to
avoid partial updates and leave the previous file intact on failure.
