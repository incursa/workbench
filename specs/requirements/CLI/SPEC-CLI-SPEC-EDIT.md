---
artifact_id: SPEC-CLI-SPEC-EDIT
artifact_type: specification
title: "CLI Spec Edit Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SPEC
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-SPEC-EDIT - CLI Spec Edit Command

## Purpose

Define the contract for editing specification metadata and body content.

## Scope

- `workbench spec edit`

## REQ-CLI-SPEC-EDIT-0001 `workbench spec edit`

`spec edit` MUST accept the documented artifact/path reference and metadata/body
options, update only the requested spec fields, and preserve canonical
traceability.

## REQ-CLI-SPEC-EDIT-0002 Body replacement rules

`spec edit` MUST treat `--body` and `--body-file` as mutually exclusive inputs.

## REQ-CLI-SPEC-EDIT-0003 Traceability preservation

`spec edit` MUST preserve unrelated backlinks and front matter fields while
updating the selected spec fields.

## REQ-CLI-SPEC-EDIT-0004 Reference resolution

`spec edit` MUST reject ambiguous artifact or path references before writing
changes.

## REQ-CLI-SPEC-EDIT-0005 Artifact identity

`spec edit` MUST keep the specification's artifact identity aligned with the
file it edits.

## REQ-CLI-SPEC-EDIT-0006 Missing-target handling

`spec edit` MUST fail clearly when the referenced specification cannot be
resolved.
