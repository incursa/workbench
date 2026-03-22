---
artifact_id: SPEC-CLI-DOC-NEW
artifact_type: specification
title: "CLI Doc New Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-NEW - CLI Doc New Command

## Purpose

Define the contract for creating canonical documentation files.

## Scope

- `workbench doc new`

## REQ-CLI-DOC-NEW-0001 `workbench doc new`

`doc new` MUST accept the documented type, title, path, artifact-id, domain,
capability, work-item, and code-ref options, create the doc with Workbench
front matter, and link any requested work items.

## REQ-CLI-DOC-NEW-0002 Path and identity rules

`doc new` MUST honor the requested destination path when supplied.

## REQ-CLI-DOC-NEW-0003 Canonical identity generation

`doc new` MUST derive canonical artifact IDs consistently from the selected
domain and capability metadata when it generates one.

## REQ-CLI-DOC-NEW-0004 Overwrite protection

`doc new` MUST refuse to overwrite an existing file unless `--force` is set.

## REQ-CLI-DOC-NEW-0005 Required front matter

`doc new` MUST populate required front matter fields even when optional
metadata is omitted.
