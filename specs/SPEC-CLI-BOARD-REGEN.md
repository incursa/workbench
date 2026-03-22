---
artifact_id: SPEC-CLI-BOARD-REGEN
artifact_type: specification
title: "CLI Board Regen Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-BOARD
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-BOARD-REGEN - CLI Board Regen Command

## Purpose

Define the contract for regenerating the workboard section.

## Scope

- `workbench board regen`

## REQ-CLI-BOARD-REGEN-0001 `workbench board regen`

`board regen` MUST regenerate only the workboard section in `work/README.md`,
respect the documented global options, and report the resulting workboard
changes without touching unrelated repo content.

## REQ-CLI-BOARD-REGEN-0002 Deterministic output

`board regen` MUST produce deterministic output for the same repository state
and avoid rewriting the workboard section when the generated content is already
current.

## REQ-CLI-BOARD-REGEN-0003 Failure handling

`board regen` MUST fail clearly when the workboard cannot be derived from the
current repository state and leaves unrelated files unchanged on failure.

## REQ-CLI-BOARD-REGEN-0004 Output target

`board regen` MUST write its derived output only to `work/README.md`.

## REQ-CLI-BOARD-REGEN-0005 Current-state reporting

`board regen` MUST report whether the workboard content changed during the
invocation.

## REQ-CLI-BOARD-REGEN-0006 Content parity

`board regen` MUST keep the generated workboard content aligned with the live
repository state.
