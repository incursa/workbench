---
artifact_id: SPEC-CLI-NAV-SYNC
artifact_type: specification
title: "CLI Nav Sync Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-NAV
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-NAV-SYNC - CLI Nav Sync Command

## Purpose

Define the contract for regenerating navigation views and canonical indexes.

## Scope

- `workbench nav sync`

## REQ-CLI-NAV-SYNC-0001 `workbench nav sync`

`nav sync` MUST accept the documented sync options, regenerate derived indexes
and derived views, and sync links first by default.

## REQ-CLI-NAV-SYNC-0002 Derived output scope

`nav sync` MUST only rewrite derived navigation outputs and leaves canonical
authored content unchanged.

## REQ-CLI-NAV-SYNC-0003 Dry-run behavior

`nav sync` MUST report the derived changes it would make when `--dry-run` is
set and leave files unchanged.

## REQ-CLI-NAV-SYNC-0004 Output safety

`nav sync` MUST leave authored documents untouched outside the derived index
and other derived outputs.

## REQ-CLI-NAV-SYNC-0005 Stage order

`nav sync` MUST reconcile links before regenerating the derived views when
links are out of date.

## REQ-CLI-NAV-SYNC-0006 Complete regeneration

`nav sync` MUST regenerate all documented indexes and derived views when the
caller selects the full nav sync path.
