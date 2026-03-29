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
  - SPEC-CLI-QUALITY-ATTEST
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

## REQ-CLI-QUALITY-0003 Display, sync, and attest separation

The `quality` family MUST keep `show`, `sync`, and `attest` separate so one
displays normalized quality evidence, one ingests raw results, and one
produces a derived attestation snapshot.

## REQ-CLI-QUALITY-0004 Advisory handling

Generated quality evidence MUST remain advisory and not be treated as an
enforced merge gate by the command family itself.

## REQ-CLI-QUALITY-0005 Child exposure

The `quality` index MUST expose `show`, `sync`, and `attest` as its documented
children.

## REQ-CLI-QUALITY-0006 Derived snapshot boundary

The `quality` family MUST keep attestation output derived and read-only so it
does not mutate canonical requirements, work items, or verification artifacts.

## Command Family Catalog

- [`SPEC-CLI-QUALITY-ATTEST`](SPEC-CLI-QUALITY-ATTEST.md)
- [`SPEC-CLI-QUALITY-SHOW`](SPEC-CLI-QUALITY-SHOW.md)
- [`SPEC-CLI-QUALITY-SYNC`](SPEC-CLI-QUALITY-SYNC.md)
