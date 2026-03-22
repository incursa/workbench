---
artifact_id: SPEC-CLI-SPEC-SHOW
artifact_type: specification
title: "CLI Spec Show Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SPEC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-SPEC-SHOW - CLI Spec Show Command

## Purpose

Define the contract for rendering a specification without mutation.

## Scope

- `workbench spec show`

## REQ-CLI-SPEC-SHOW-0001 `workbench spec show`

`spec show` MUST accept a spec reference and render the spec metadata and body
without mutating the file.

## REQ-CLI-SPEC-SHOW-0002 Read-only rendering

`spec show` MUST never rewrite the spec file as a side effect of rendering it.

## REQ-CLI-SPEC-SHOW-0003 Resolution behavior

`spec show` MUST prefer the referenced artifact ID when both ID and path are
available.
