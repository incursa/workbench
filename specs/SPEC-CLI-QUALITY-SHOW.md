---
artifact_id: SPEC-CLI-QUALITY-SHOW
artifact_type: specification
title: "CLI Quality Show Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-QUALITY
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-QUALITY-SHOW - CLI Quality Show Command

## Purpose

Define the contract for rendering normalized quality evidence.

## Scope

- `workbench quality show`

## REQ-CLI-QUALITY-SHOW-0001 `workbench quality show`

`quality show` MUST accept the documented kind and path options, render the
latest normalized quality artifact or a selected artifact, and support
machine-readable output.

## REQ-CLI-QUALITY-SHOW-0002 Kind resolution

`quality show` MUST resolve the selected artifact kind consistently across the
normalized quality report, inventory, results, and coverage outputs.

## REQ-CLI-QUALITY-SHOW-0003 Read-only behavior

`quality show` MUST not write normalized artifacts or alter the quality cache.

## REQ-CLI-QUALITY-SHOW-0004 Default artifact selection

`quality show` MUST fall back to the latest normalized artifact when no path
is supplied.

## REQ-CLI-QUALITY-SHOW-0005 Output identity

`quality show` MUST include the selected artifact kind and path in its output
when those values are available.

## REQ-CLI-QUALITY-SHOW-0006 Machine-readable output

`quality show` MUST support machine-readable output when requested.
