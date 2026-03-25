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
  - WI-WB-0024
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
or to [`specs/generated/commands.md`](../../generated/commands.md) when no path is supplied.

## REQ-CLI-DOC-REGEN-HELP-0003 Check mode

`doc regen-help` MUST leave files unchanged when `--check` is set and the
snapshot is already current.

## REQ-CLI-DOC-REGEN-HELP-0004 Snapshot parity

`doc regen-help` MUST preserve the live command tree ordering and naming in
the generated snapshot.

## REQ-CLI-DOC-REGEN-HELP-0005 Complete coverage

`doc regen-help` MUST include every exposed command and subcommand in the
generated snapshot.

## REQ-CLI-DOC-REGEN-HELP-0006 Change reporting

`doc regen-help` MUST report whether the checked-in snapshot changed during the
invocation.

## REQ-CLI-DOC-REGEN-HELP-0007 Failure safety

`doc regen-help` MUST leave the existing snapshot unchanged when generation
fails.
