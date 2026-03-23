---
artifact_id: SPEC-CLI-MIGRATE
artifact_type: specification
title: "CLI Migrate Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-OPERATIONS
  - WI-WB-0024
---

# SPEC-CLI-MIGRATE - CLI Migrate Command

## Purpose

Define the contract for repository migration actions.

## Scope

- `workbench migrate`

## REQ-CLI-MIGRATE-0001 `workbench migrate`

`migrate` MUST accept the documented migration target and dry-run option,
perform only the supported repository migration(s), and reject unknown targets
with a clear error.

## REQ-CLI-MIGRATE-0002 Dry-run semantics

`migrate` MUST leave the repository unchanged in dry-run mode while still
reporting the actions that would have been applied.

## REQ-CLI-MIGRATE-0003 Scope control

`migrate` MUST never perform migrations beyond the supported target set for the
current tool version.

## REQ-CLI-MIGRATE-0004 Unknown-target handling

`migrate` MUST reject unsupported migration targets before changing files.

## REQ-CLI-MIGRATE-0005 Idempotence

`migrate` MUST be safe to rerun when the selected migration has already been
applied.
