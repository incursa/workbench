---
artifact_id: SPEC-CLI-DOC-REGEN-HELP
artifact_type: specification
title: "CLI Doc Regen-Help Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-REGEN-HELP - CLI Doc Regen-Help Command

## Purpose

Define the contract for regenerating the CLI help snapshot.

## Scope

- `workbench doc regen-help`

## REQ-CLI-DOC-REGEN-HELP-0001 `workbench doc regen-help`

`doc regen-help` MUST regenerate the CLI help snapshot from the live command
tree and, with `--check`, fail if the checked-in snapshot is stale.

## REQ-CLI-DOC-REGEN-HELP-0002 Output path behavior

`doc regen-help` MUST write the generated help snapshot to the requested path
or to `contracts/commands.md` when no path is supplied.

## REQ-CLI-DOC-REGEN-HELP-0003 Check mode

`doc regen-help` MUST leave files unchanged when `--check` is set and the
snapshot is already current.
