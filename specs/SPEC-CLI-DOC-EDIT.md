---
artifact_id: SPEC-CLI-DOC-EDIT
artifact_type: specification
title: "CLI Doc Edit Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-EDIT - CLI Doc Edit Command

## Purpose

Define the contract for editing documentation metadata and body content.

## Scope

- `workbench doc edit`

## REQ-CLI-DOC-EDIT-0001 `workbench doc edit`

`doc edit` MUST accept the documented artifact/path reference and metadata/body
options, update only the requested fields, and preserve canonical front matter
and backlink alignment.

## REQ-CLI-DOC-EDIT-0002 Body replacement rules

`doc edit` MUST replace the body only when `--body` or `--body-file` is
supplied and does not combine both inputs in a single invocation.

## REQ-CLI-DOC-EDIT-0003 Traceability preservation

`doc edit` MUST preserve unrelated backlinks and metadata fields while
updating the requested document fields.

## REQ-CLI-DOC-EDIT-0004 Reference resolution

`doc edit` MUST reject ambiguous artifact or path references before writing
changes.
