---
artifact_id: SPEC-CLI-OPERATIONS
artifact_type: specification
title: "CLI Operational Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-MIGRATE
  - SPEC-CLI-NORMALIZE
  - SPEC-CLI-PROMOTE
  - SPEC-CLI-SCAFFOLD
  - SPEC-CLI-SYNC
  - SPEC-CLI-VALIDATE
  - SPEC-CLI-VERSION
  - TASK-0024
---

# SPEC-CLI-OPERATIONS - CLI Operational Commands Index

## Purpose

Define the navigation index for top-level operational commands that do not
belong to the onboarding or command-family specs.

## Scope

- `workbench migrate`
- `workbench normalize`
- `workbench promote`
- `workbench scaffold`
- `workbench sync`
- `workbench validate`
- `workbench version`

## REQ-CLI-OPERATIONS-0001 Index coverage

This index MUST point at the dedicated command specs for each operational
command and stay in sync with the live command tree.

## REQ-CLI-OPERATIONS-0002 Root command boundary

The operational command index MUST keep the grouped root commands separated
from leaf command contracts and leaves behavior owned by another spec alone.

## REQ-CLI-OPERATIONS-0003 Top-level boundary

The operations index MUST contain only top-level operational commands and avoid
duplicating family-specific command contracts.

## REQ-CLI-OPERATIONS-0004 Sync separation

The operations grouping MUST keep `sync`, `validate`, and `version` distinct
from onboarding and from the dedicated command-family specs.

## REQ-CLI-OPERATIONS-0005 Child exposure

The operations index MUST expose only the documented top-level operational
commands.

## Command Family Catalog

- [SPEC-CLI-MIGRATE](./SPEC-CLI-MIGRATE.md)
- [SPEC-CLI-NORMALIZE](./SPEC-CLI-NORMALIZE.md)
- [SPEC-CLI-PROMOTE](./SPEC-CLI-PROMOTE.md)
- [SPEC-CLI-SCAFFOLD](./SPEC-CLI-SCAFFOLD.md)
- [SPEC-CLI-SYNC](./SPEC-CLI-SYNC.md)
- [SPEC-CLI-VALIDATE](./SPEC-CLI-VALIDATE.md)
- [SPEC-CLI-VERSION](./SPEC-CLI-VERSION.md)
