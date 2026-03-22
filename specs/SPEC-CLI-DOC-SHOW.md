---
artifact_id: SPEC-CLI-DOC-SHOW
artifact_type: specification
title: "CLI Doc Show Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-DOC-SHOW - CLI Doc Show Command

## Purpose

Define the contract for rendering a documentation file without mutation.

## Scope

- `workbench doc show`

## REQ-CLI-DOC-SHOW-0001 `workbench doc show`

`doc show` MUST resolve the referenced document by artifact ID or path and
render its metadata and body without mutating the file.

## REQ-CLI-DOC-SHOW-0002 Read-only rendering

`doc show` MUST never rewrite the document, even if normalization would be
possible.

## REQ-CLI-DOC-SHOW-0003 Resolution fallback

`doc show` MUST prefer artifact ID resolution when both an ID and a path point
to the same document.
