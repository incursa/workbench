---
artifact_id: SPEC-CLI-DOC
artifact_type: specification
title: "CLI Documentation Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-DOC-DELETE
  - SPEC-CLI-DOC-EDIT
  - SPEC-CLI-DOC-LINK
  - SPEC-CLI-DOC-NEW
  - SPEC-CLI-DOC-REGEN-HELP
  - SPEC-CLI-DOC-SHOW
  - SPEC-CLI-DOC-SUMMARIZE
  - SPEC-CLI-DOC-SYNC
  - SPEC-CLI-DOC-UNLINK
  - WI-WB-0024
---

# SPEC-CLI-DOC - CLI Documentation Commands Index

## Purpose

Define the navigation index for documentation authoring, linking, syncing, and
transcript-driven doc generation commands.

## Scope

- `workbench doc`

## REQ-CLI-DOC-0001 Index coverage

The `doc` index MUST point at the dedicated leaf specs for each exposed
documentation command and stay in sync with the live command tree.

## REQ-CLI-DOC-0002 Group-root behavior

The `doc` group root MUST remain non-mutating and routes document actions to
the dedicated leaf commands.

## REQ-CLI-DOC-0003 Snapshot boundary

`doc regen-help` MUST regenerate `specs/generated/commands.md` from the live command
tree and leave authored docs untouched.

## REQ-CLI-DOC-0004 Metadata boundary

The `doc` family MUST keep front matter and work-item backlinks as leaf-command
responsibilities rather than redefining those rules at the index level.

## REQ-CLI-DOC-0005 Child exposure

The `doc` index MUST expose the documented authoring, linking, syncing, and
inspection commands as its children.

## REQ-CLI-DOC-0006 Root boundary

The `doc` group root MUST keep regeneration separate from authored document
content.

## REQ-CLI-DOC-0007 Family separation

The `doc` index MUST keep authoring, inspection, and snapshot-generation
commands in separate leaf families.

## Command Family Catalog

- [SPEC-CLI-DOC-DELETE](./SPEC-CLI-DOC-DELETE.md)
- [SPEC-CLI-DOC-EDIT](./SPEC-CLI-DOC-EDIT.md)
- [SPEC-CLI-DOC-LINK](./SPEC-CLI-DOC-LINK.md)
- [SPEC-CLI-DOC-NEW](./SPEC-CLI-DOC-NEW.md)
- [SPEC-CLI-DOC-REGEN-HELP](./SPEC-CLI-DOC-REGEN-HELP.md)
- [SPEC-CLI-DOC-SHOW](./SPEC-CLI-DOC-SHOW.md)
- [SPEC-CLI-DOC-SUMMARIZE](./SPEC-CLI-DOC-SUMMARIZE.md)
- [SPEC-CLI-DOC-SYNC](./SPEC-CLI-DOC-SYNC.md)
- [SPEC-CLI-DOC-UNLINK](./SPEC-CLI-DOC-UNLINK.md)
