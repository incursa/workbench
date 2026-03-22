---
artifact_id: SPEC-CLI-BOARD
artifact_type: specification
title: "CLI Board Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-BOARD-REGEN
  - TASK-0024
---

# SPEC-CLI-BOARD - CLI Board Commands Index

## Purpose

Define the navigation index for board-related CLI commands.

## Scope

- `workbench board`

## REQ-CLI-BOARD-0001 Index coverage

The `board` index MUST point at the dedicated leaf spec for the exposed board
subcommand and stay in sync with the live command tree.

## REQ-CLI-BOARD-0002 Group-root behavior

The `board` group root MUST remain non-mutating and leaves hidden subcommands
out of the documented tree.

## REQ-CLI-BOARD-0003 Regeneration scope

`board regen` MUST regenerate only the workboard section in `work/README.md`.

## REQ-CLI-BOARD-0004 Output containment

`board regen` MUST preserve the rest of `work/README.md` unchanged except for
the required workboard refresh.

## REQ-CLI-BOARD-0005 Child exposure

The `board` index MUST expose `regen` as its only documented child command.

## Command Family Catalog

- [SPEC-CLI-BOARD-REGEN](./SPEC-CLI-BOARD-REGEN.md)
