---
artifact_id: SPEC-CLI-CODEX-RUN
artifact_type: specification
title: "CLI Codex Run Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-CODEX
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-CODEX-RUN - CLI Codex Run Command

## Purpose

Define the contract for launching Codex prompts.

## Scope

- `workbench codex run`

## REQ-CLI-CODEX-RUN-0001 `workbench codex run`

`codex run` MUST accept the documented prompt and terminal options, launch
Codex in the requested mode, and surface the exit status and captured output
without hiding failures.

## REQ-CLI-CODEX-RUN-0002 Prompt contract

`codex run` MUST reject empty prompts and preserve the original prompt text
when it forwards the request to Codex.

## REQ-CLI-CODEX-RUN-0003 Terminal behavior

`codex run` MUST either attach to the current terminal for captured output or
open a separate terminal window when `--terminal` is set, without silently
changing modes.

## REQ-CLI-CODEX-RUN-0004 Exit propagation

`codex run` MUST return Codex's exit status without translating failures into
success.

## REQ-CLI-CODEX-RUN-0005 Prompt visibility

`codex run` MUST preserve the original prompt text in the forwarded request.

## REQ-CLI-CODEX-RUN-0006 Mode reporting

`codex run` MUST report the execution mode it selected when it starts Codex.

## REQ-CLI-CODEX-RUN-0007 Machine-readable output

`codex run` MUST support machine-readable output for the launch result when
requested.
