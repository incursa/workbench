---
artifact_id: SPEC-CLI-QUALITY-SYNC
artifact_type: specification
title: "CLI Quality Sync Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-QUALITY
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-QUALITY-SYNC - CLI Quality Sync Command

## Purpose

Define the contract for collecting and normalizing quality evidence.

## Scope

- `workbench quality sync`

## REQ-CLI-QUALITY-SYNC-0001 `workbench quality sync`

`quality sync` MUST accept the documented contract, results, coverage, out-dir,
and dry-run options, discover and normalize the observed testing evidence, and
generate the current quality report.

## REQ-CLI-QUALITY-SYNC-0002 Evidence ingestion

`quality sync` MUST scan the provided results and coverage locations
recursively when directories are supplied and ignores unrelated files.

## REQ-CLI-QUALITY-SYNC-0003 Dry-run behavior

`quality sync` MUST compute the report without writing artifacts when
`--dry-run` is set.

## REQ-CLI-QUALITY-SYNC-0004 Output directory scope

`quality sync` MUST write generated artifacts only beneath the requested
output directory.
