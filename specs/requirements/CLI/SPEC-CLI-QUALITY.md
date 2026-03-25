---
artifact_id: SPEC-CLI-QUALITY
artifact_type: specification
title: "CLI Quality Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-QUALITY-SHOW
  - SPEC-CLI-QUALITY-SYNC
  - WI-WB-0024
---

# SPEC-CLI-QUALITY - CLI Quality Commands Index

## Purpose

Define the navigation index for quality evidence capture and reporting
commands.

## Scope

- `workbench quality`

## REQ-CLI-QUALITY-0001 Index coverage

The `quality` index MUST point at the dedicated leaf specs for each exposed
quality command and stay in sync with the live command tree.

## REQ-CLI-QUALITY-0002 Advisory-report boundary

The `quality` group root MUST keep the generated report advisory rather than
merge-blocking.

## REQ-CLI-QUALITY-0003 Display and sync separation

The `quality` family MUST keep `show` and `sync` separate so one displays
quality evidence and the other ingests raw results.

## REQ-CLI-QUALITY-0004 Advisory handling

Generated quality evidence MUST remain advisory and not be treated as an
enforced merge gate by the command family itself.

## REQ-CLI-QUALITY-0005 Child exposure

The `quality` index MUST expose `show` and `sync` as its documented children.

## Command Family Catalog

- [`SPEC-CLI-QUALITY-SHOW`](SPEC-CLI-QUALITY-SHOW.md)
- [`SPEC-CLI-QUALITY-SYNC`](SPEC-CLI-QUALITY-SYNC.md)
