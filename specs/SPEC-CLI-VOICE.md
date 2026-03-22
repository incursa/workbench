---
artifact_id: SPEC-CLI-VOICE
artifact_type: specification
title: "CLI Voice Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-VOICE-DOC
  - SPEC-CLI-VOICE-WORKITEM
  - TASK-0024
---

# SPEC-CLI-VOICE - CLI Voice Commands Index

## Purpose

Define the navigation index for transcript-driven doc and work-item creation
commands.

## Scope

- `workbench voice`

## REQ-CLI-VOICE-0001 Index coverage

The `voice` index MUST point at the dedicated leaf specs for each exposed
voice command and stay in sync with the live command tree.

## REQ-CLI-VOICE-0002 Input mechanism boundary

The `voice` group root MUST treat transcription as input generation only and
must not add a separate persistence model.

## REQ-CLI-VOICE-0003 Leaf exposure

The `voice` index MUST expose `doc` and `workitem` as its only documented
children.

## REQ-CLI-VOICE-0004 Transcription boundary

Voice input MUST produce only the targeted artifact type and not create hidden
persistence surfaces.

## REQ-CLI-VOICE-0005 Read-only root behavior

The `voice` family root MUST remain read-only and route mutations to the
documented leaf commands.

## REQ-CLI-VOICE-0006 Child exposure

The `voice` index MUST expose only the documented `doc` and `workitem`
children.

## Command Family Catalog

- [SPEC-CLI-VOICE-DOC](./SPEC-CLI-VOICE-DOC.md)
- [SPEC-CLI-VOICE-WORKITEM](./SPEC-CLI-VOICE-WORKITEM.md)
