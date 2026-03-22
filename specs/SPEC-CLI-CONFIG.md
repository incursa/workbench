---
artifact_id: SPEC-CLI-CONFIG
artifact_type: specification
title: "CLI Configuration Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-CONFIG-CREDENTIALS
  - SPEC-CLI-CONFIG-CREDENTIALS-SET
  - SPEC-CLI-CONFIG-CREDENTIALS-UNSET
  - SPEC-CLI-CONFIG-SET
  - SPEC-CLI-CONFIG-SHOW
  - TASK-0024
---

# SPEC-CLI-CONFIG - CLI Configuration Commands Index

## Purpose

Define the navigation index for configuration and credential management
commands.

## Scope

- `workbench config`

## REQ-CLI-CONFIG-0001 Index coverage

The `config` index MUST point at the dedicated leaf specs for each exposed
configuration command and stay in sync with the live command tree.

## REQ-CLI-CONFIG-0002 Group-root behavior

The `config` group root MUST remain non-mutating and leaves repo config and
credentials files untouched by itself.

## REQ-CLI-CONFIG-0003 Precedence boundary

`config show` MUST resolve defaults, repository config, and CLI overrides in
the documented precedence order.

## REQ-CLI-CONFIG-0004 File-target boundary

`config set` and the credential subcommands MUST confine writes to
`.workbench/config.json` or `credentials.env` as appropriate, leaving
unrelated files untouched.

## Command Family Catalog

- [SPEC-CLI-CONFIG-CREDENTIALS](./SPEC-CLI-CONFIG-CREDENTIALS.md)
- [SPEC-CLI-CONFIG-CREDENTIALS-SET](./SPEC-CLI-CONFIG-CREDENTIALS-SET.md)
- [SPEC-CLI-CONFIG-CREDENTIALS-UNSET](./SPEC-CLI-CONFIG-CREDENTIALS-UNSET.md)
- [SPEC-CLI-CONFIG-SET](./SPEC-CLI-CONFIG-SET.md)
- [SPEC-CLI-CONFIG-SHOW](./SPEC-CLI-CONFIG-SHOW.md)
