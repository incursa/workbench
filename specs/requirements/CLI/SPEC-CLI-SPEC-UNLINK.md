---
artifact_id: SPEC-CLI-SPEC-UNLINK
artifact_type: specification
title: "CLI Spec Unlink Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SPEC
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-SPEC-UNLINK - CLI Spec Unlink Command

## Purpose

Define the contract for removing selected backlinks from specs.

## Scope

- `workbench spec unlink`

## REQ-CLI-SPEC-UNLINK-0001 `workbench spec unlink`

`spec unlink` MUST accept the spec reference and work-item list, remove only
the selected backlinks, and preserve the rest of the document state.

## REQ-CLI-SPEC-UNLINK-0002 Selective removal

`spec unlink` MUST leave unrelated backlinks intact.

## REQ-CLI-SPEC-UNLINK-0003 Dry-run behavior

`spec unlink` MUST report backlink removals without writing files when
`--dry-run` is set.

## REQ-CLI-SPEC-UNLINK-0004 Idempotence

`spec unlink` MUST ignore backlinks that are already absent.

## REQ-CLI-SPEC-UNLINK-0005 Atomic removal

`spec unlink` MUST leave the specification unchanged if backlink removal
fails.

## REQ-CLI-SPEC-UNLINK-0006 Link summary

`spec unlink` MUST report which backlinks were removed from the specification.
