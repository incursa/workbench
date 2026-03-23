---
artifact_id: SPEC-CLI-SPEC-LINK
artifact_type: specification
title: "CLI Spec Link Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SPEC
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-SPEC-LINK - CLI Spec Link Command

## Purpose

Define the contract for adding backlinks to specification files.

## Scope

- `workbench spec link`

## REQ-CLI-SPEC-LINK-0001 `workbench spec link`

`spec link` MUST accept the spec reference and work-item list, add the
requested backlinks, and avoid duplicate links.

## REQ-CLI-SPEC-LINK-0002 Duplicate suppression

`spec link` MUST leave already-present backlinks unchanged.

## REQ-CLI-SPEC-LINK-0003 Dry-run behavior

`spec link` MUST report backlink changes without writing files when `--dry-run`
is set.

## REQ-CLI-SPEC-LINK-0004 Reference validation

`spec link` MUST reject unknown backlink references before writing changes.

## REQ-CLI-SPEC-LINK-0005 Target validation

`spec link` MUST validate every requested work-item reference before writing
any changes.

## REQ-CLI-SPEC-LINK-0006 Link ordering

`spec link` MUST keep backlink ordering stable when it appends new links.
