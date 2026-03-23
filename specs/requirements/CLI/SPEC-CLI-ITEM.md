---
artifact_id: SPEC-CLI-ITEM
artifact_type: specification
title: "CLI Work Item Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-ITEM-CLOSE
  - SPEC-CLI-ITEM-DELETE
  - SPEC-CLI-ITEM-EDIT
  - SPEC-CLI-ITEM-GENERATE
  - SPEC-CLI-ITEM-IMPORT
  - SPEC-CLI-ITEM-LINK
  - SPEC-CLI-ITEM-LIST
  - SPEC-CLI-ITEM-MOVE
  - SPEC-CLI-ITEM-NEW
  - SPEC-CLI-ITEM-NORMALIZE
  - SPEC-CLI-ITEM-RENAME
  - SPEC-CLI-ITEM-SHOW
  - SPEC-CLI-ITEM-STATUS
  - SPEC-CLI-ITEM-SYNC
  - SPEC-CLI-ITEM-UNLINK
  - WI-WB-0024
---

# SPEC-CLI-ITEM - CLI Work Item Commands Index

## Purpose

Define the navigation index for work-item creation, mutation, synchronization,
and query commands.

## Scope

- `workbench item`

## REQ-CLI-ITEM-0001 Index coverage

The `item` index MUST point at the dedicated leaf specs for each exposed work
item command and stay in sync with the live command tree.

## REQ-CLI-ITEM-0002 Command-tree alignment

The `item` index MUST keep the documented work-item command set aligned with
the live command tree, including leaf command names and help visibility.

## REQ-CLI-ITEM-0003 Mutation boundary

The `item` group root MUST route create, edit, delete, move, link, sync, and
other write actions to leaf commands and leave direct mutation to those leaf
commands.

## REQ-CLI-ITEM-0004 Query boundary

The `item` group root MUST keep lookup and listing behavior in the documented
leaf commands instead of implementing item reads itself.

## REQ-CLI-ITEM-0005 Child exposure

The `item` index MUST expose only the documented work-item leaf commands.

## REQ-CLI-ITEM-0006 Root boundary

The `item` index MUST keep command behavior in leaf specs rather than
defining item actions at the index level.

## REQ-CLI-ITEM-0007 Family separation

The `item` index MUST keep mutation commands separate from query and
reconciliation commands.

## Command Family Catalog

- [SPEC-CLI-ITEM-CLOSE](./SPEC-CLI-ITEM-CLOSE.md)
- [SPEC-CLI-ITEM-DELETE](./SPEC-CLI-ITEM-DELETE.md)
- [SPEC-CLI-ITEM-EDIT](./SPEC-CLI-ITEM-EDIT.md)
- [SPEC-CLI-ITEM-GENERATE](./SPEC-CLI-ITEM-GENERATE.md)
- [SPEC-CLI-ITEM-IMPORT](./SPEC-CLI-ITEM-IMPORT.md)
- [SPEC-CLI-ITEM-LINK](./SPEC-CLI-ITEM-LINK.md)
- [SPEC-CLI-ITEM-LIST](./SPEC-CLI-ITEM-LIST.md)
- [SPEC-CLI-ITEM-MOVE](./SPEC-CLI-ITEM-MOVE.md)
- [SPEC-CLI-ITEM-NEW](./SPEC-CLI-ITEM-NEW.md)
- [SPEC-CLI-ITEM-NORMALIZE](./SPEC-CLI-ITEM-NORMALIZE.md)
- [SPEC-CLI-ITEM-RENAME](./SPEC-CLI-ITEM-RENAME.md)
- [SPEC-CLI-ITEM-SHOW](./SPEC-CLI-ITEM-SHOW.md)
- [SPEC-CLI-ITEM-STATUS](./SPEC-CLI-ITEM-STATUS.md)
- [SPEC-CLI-ITEM-SYNC](./SPEC-CLI-ITEM-SYNC.md)
- [SPEC-CLI-ITEM-UNLINK](./SPEC-CLI-ITEM-UNLINK.md)
