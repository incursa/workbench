---
artifact_id: SPEC-CLI-VOICE-WORKITEM
artifact_type: specification
title: "CLI Voice Workitem Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-VOICE
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-VOICE-WORKITEM - CLI Voice Workitem Command

## Purpose

Define the contract for creating work items from voice input.

## Scope

- `workbench voice workitem`

## REQ-CLI-VOICE-WORKITEM-0001 `workbench voice workitem`

`voice workitem` MUST accept the documented type, status, priority, and owner
options, record and transcribe voice input, and create a work item from the
transcript.

## REQ-CLI-VOICE-WORKITEM-0002 Defaults and ownership

`voice workitem` MUST default the type from AI choice when not supplied and
must carry the requested status, priority, and owner metadata into the new
item.

## REQ-CLI-VOICE-WORKITEM-0003 Canonical item output

`voice workitem` MUST create a canonical work-item file that follows the same
structure as `workbench item new`.

## REQ-CLI-VOICE-WORKITEM-0004 Transcript title selection

`voice workitem` MUST allow the transcript to supply the title when one is not
passed explicitly.

## REQ-CLI-VOICE-WORKITEM-0005 Transcription failure handling

`voice workitem` MUST fail clearly when transcription cannot be captured.
