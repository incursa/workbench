---
artifact_id: SPEC-CLI-ITEM-GENERATE
artifact_type: specification
title: "CLI Item Generate Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-ITEM-GENERATE - CLI Item Generate Command

## Purpose

Define the contract for AI-assisted work-item drafting.

## Scope

- `workbench item generate`

## REQ-CLI-ITEM-GENERATE-0001 `workbench item generate`

`item generate` MUST accept the documented prompt, type, status, priority, and
owner options, turn the prompt into a work-item draft through the AI backend,
and create the resulting item in the repo.

## REQ-CLI-ITEM-GENERATE-0002 Prompt fidelity

`item generate` MUST preserve the user prompt as the generation source and
must not silently substitute a different work item topic.

## REQ-CLI-ITEM-GENERATE-0003 Canonical draft creation

`item generate` MUST create the output as a canonical work-item draft with the
same front matter and section structure as `workbench item new`.

## REQ-CLI-ITEM-GENERATE-0004 AI failure handling

`item generate` MUST surface AI backend failures before creating a work item
and leave the repository unchanged when generation fails.

## REQ-CLI-ITEM-GENERATE-0005 Single-item output

`item generate` MUST create exactly one work item per invocation.

## REQ-CLI-ITEM-GENERATE-0006 Draft validation

`item generate` MUST validate the generated draft against the canonical
work-item structure before creating the file.

## REQ-CLI-ITEM-GENERATE-0007 Output reporting

`item generate` MUST report the created work-item ID and file path after a
successful invocation.
