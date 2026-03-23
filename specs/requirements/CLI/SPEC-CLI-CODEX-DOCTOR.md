---
artifact_id: SPEC-CLI-CODEX-DOCTOR
artifact_type: specification
title: "CLI Codex Doctor Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-CODEX
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-CODEX-DOCTOR - CLI Codex Doctor Command

## Purpose

Define the contract for checking Codex availability.

## Scope

- `workbench codex doctor`

## REQ-CLI-CODEX-DOCTOR-0001 `workbench codex doctor`

`codex doctor` MUST report whether Codex is installed and callable, returning a
machine-readable status when `--format json` is requested.

## REQ-CLI-CODEX-DOCTOR-0002 Missing dependency reporting

`codex doctor` MUST explain whether the failure is due to a missing binary,
missing PATH entry, or a non-callable installation so the caller can fix the
environment without guessing.

## REQ-CLI-CODEX-DOCTOR-0003 Non-mutating behavior

`codex doctor` MUST never mutate repository files or configuration state.

## REQ-CLI-CODEX-DOCTOR-0004 Exit contract

`codex doctor` MUST return success only when Codex is available and callable.

## REQ-CLI-CODEX-DOCTOR-0005 Machine-readable status

`codex doctor` MUST include the availability state in its machine-readable
output when `--format json` is requested.
