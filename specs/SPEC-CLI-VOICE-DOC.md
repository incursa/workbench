---
artifact_id: SPEC-CLI-VOICE-DOC
artifact_type: specification
title: "CLI Voice Doc Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-VOICE
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-VOICE-DOC - CLI Voice Doc Command

## Purpose

Define the contract for creating documentation from voice input.

## Scope

- `workbench voice doc`

## REQ-CLI-VOICE-DOC-0001 `workbench voice doc`

`voice doc` MUST accept the documented type, out, and title options, record and
transcribe voice input, and create a doc with YAML front matter from the
transcript.

## REQ-CLI-VOICE-DOC-0002 Type and path defaults

`voice doc` MUST honor the supported doc types and derives the output path from
the selected type when `--out` is omitted.

## REQ-CLI-VOICE-DOC-0003 Transcript-to-doc fidelity

`voice doc` MUST preserve the transcribed content in the generated body and
must attach the requested title when supplied.

## REQ-CLI-VOICE-DOC-0004 Output path resolution

`voice doc` MUST derive the output path from the selected doc type when
`--out` is omitted.
