---
artifact_id: SPEC-CLI-CONFIG-CREDENTIALS
artifact_type: specification
title: "CLI Config Credentials Group"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-CONFIG
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-CONFIG-CREDENTIALS - CLI Config Credentials Group

## Purpose

Define the contract for the credentials grouping command.

## Scope

- `workbench config credentials`

## REQ-CLI-CONFIG-CREDENTIALS-0001 `workbench config credentials`

`config credentials` MUST act as a pure grouping command for credential file
management and not mutate state by itself.

## REQ-CLI-CONFIG-CREDENTIALS-0002 Subcommand exposure

`config credentials` MUST expose the documented `set` and `unset` subcommands
in help output and keep that tree aligned with the live CLI.

## REQ-CLI-CONFIG-CREDENTIALS-0003 Credential-file scope

`config credentials` MUST operate only on `credentials.env` entries and leave
repository config values unchanged.

## REQ-CLI-CONFIG-CREDENTIALS-0004 Child-command contract

`config credentials set` and `config credentials unset` MUST remain the only
documented children of this grouping command.

## REQ-CLI-CONFIG-CREDENTIALS-0005 Help ordering

The `config credentials` help output MUST list `set` before `unset`.
