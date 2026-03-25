---
artifact_id: SPEC-CLI-SPEC
artifact_type: specification
title: "CLI Specification Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-SPEC-DELETE
  - SPEC-CLI-SPEC-EDIT
  - SPEC-CLI-SPEC-LINK
  - SPEC-CLI-SPEC-NEW
  - SPEC-CLI-SPEC-SHOW
  - SPEC-CLI-SPEC-SYNC
  - SPEC-CLI-SPEC-UNLINK
  - WI-WB-0024
---

# SPEC-CLI-SPEC - CLI Specification Commands Index

## Purpose

Define the navigation index for specification authoring, linking, and syncing
commands.

## Scope

- `workbench spec`

## REQ-CLI-SPEC-0001 Index coverage

The `spec` index MUST point at the dedicated leaf specs for each exposed
specification command and stay in sync with the live command tree.

## REQ-CLI-SPEC-0002 Authoring boundary

The `spec` group root MUST keep spec-authoring behavior aligned with the
canonical spec-trace standard and leaves files unchanged by itself.

## REQ-CLI-SPEC-0003 Leaf ownership

Each `spec` leaf command MUST own one repository metadata action, and the
index keeps mutation behavior in the leaf specs.

## REQ-CLI-SPEC-0004 Traceability boundary

`spec` commands MUST preserve spec-to-work-item backlinks and spec front matter
without touching unrelated docs.

## REQ-CLI-SPEC-0005 Child exposure

The `spec` index MUST expose `delete`, `edit`, `link`, `new`, `show`, `sync`,
and `unlink` as its documented children.

## Command Family Catalog

- [`SPEC-CLI-SPEC-DELETE`](SPEC-CLI-SPEC-DELETE.md)
- [`SPEC-CLI-SPEC-EDIT`](SPEC-CLI-SPEC-EDIT.md)
- [`SPEC-CLI-SPEC-LINK`](SPEC-CLI-SPEC-LINK.md)
- [`SPEC-CLI-SPEC-NEW`](SPEC-CLI-SPEC-NEW.md)
- [`SPEC-CLI-SPEC-SHOW`](SPEC-CLI-SPEC-SHOW.md)
- [`SPEC-CLI-SPEC-SYNC`](SPEC-CLI-SPEC-SYNC.md)
- [`SPEC-CLI-SPEC-UNLINK`](SPEC-CLI-SPEC-UNLINK.md)
