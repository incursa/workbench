---
artifact_id: SPEC-CLI-VERSION
artifact_type: specification
title: "CLI Version Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-OPERATIONS
  - TASK-0024
---

# SPEC-CLI-VERSION - CLI Version Command

## Purpose

Define the contract for reporting the CLI build version.

## Scope

- `workbench version`

## REQ-CLI-VERSION-0001 `workbench version`

`version` MUST print the CLI version from build or assembly metadata and return
success without mutating repo state.

## REQ-CLI-VERSION-0002 Output stability

`version` MUST print a single, stable version string that scripts can parse
without needing repository context.

## REQ-CLI-VERSION-0003 Non-mutating behavior

`version` MUST not read or write any repository files beyond what is needed to
load its own build metadata.
