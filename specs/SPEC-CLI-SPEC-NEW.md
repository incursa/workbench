---
artifact_id: SPEC-CLI-SPEC-NEW
artifact_type: specification
title: "CLI Spec New Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SPEC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-SPEC-NEW - CLI Spec New Command

## Purpose

Define the contract for creating canonical specification files.

## Scope

- `workbench spec new`

## REQ-CLI-SPEC-NEW-0001 `workbench spec new`

`spec new` MUST accept the documented title, path, artifact-id, domain,
capability, work-item, and code-ref options, create a canonical spec file, and
link any requested work items.

## REQ-CLI-SPEC-NEW-0002 Identity and path rules

`spec new` MUST create a canonical spec artifact ID when one is not supplied.

## REQ-CLI-SPEC-NEW-0003 Path handling

`spec new` MUST honor the requested path when it is supplied.

## REQ-CLI-SPEC-NEW-0004 Overwrite protection

`spec new` MUST refuse to overwrite an existing file unless `--force` is set.

## REQ-CLI-SPEC-NEW-0005 Required front matter

`spec new` MUST populate required canonical traceability fields even when
optional metadata is omitted.

## REQ-CLI-SPEC-NEW-0006 Repository scope

`spec new` MUST write the new specification beneath the configured specs root
or the explicit path supplied by the caller.

## REQ-CLI-SPEC-NEW-0007 Domain validation

`spec new` MUST reject unsupported domains or capabilities before writing the
file.
