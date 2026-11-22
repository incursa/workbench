---
artifact_id: SPEC-CLI-DOCTOR
artifact_type: specification
title: "CLI Doctor Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-ONBOARDING
  - WI-WB-0001
---

# SPEC-CLI-DOCTOR - CLI Doctor Command

## Purpose

Define the contract for the repository health and readiness command.

## Scope

- `workbench doctor`

## REQ-CLI-DOCTOR-0001 `workbench doctor`

`doctor` MUST inspect git availability, repo readiness, config health, and
expected paths, and return human-readable next steps by default while
supporting JSON output when requested.

## REQ-CLI-DOCTOR-0002 Output contract

`doctor` MUST keep the human-readable summary concise, actionable, and stable
enough that automation can compare successive runs.

## REQ-CLI-DOCTOR-0003 Non-mutating behavior

`doctor` MUST not change repository files, config values, or credential files
unless the caller explicitly selects `--fix`.

## REQ-CLI-DOCTOR-0004 Output stability

`doctor` MUST keep its summary phrasing concise and deterministic enough for
automation to parse when `--format json` is not selected.

## REQ-CLI-DOCTOR-0005 Machine-readable output

`doctor` MUST emit the repository readiness state in a structured format when
machine-readable output is requested. JSON output MUST include an explicit
schema version, the checks performed, repairs applied, and at most one exact
recommended next action.

## REQ-CLI-DOCTOR-0006 Check coverage summary

`doctor` MUST summarize git, config, expected paths, generated navigation,
GitHub, OpenAI, Codex, and voice-transcription readiness in its reported
results.

## REQ-CLI-DOCTOR-0007 Deterministic repairs

`doctor --fix` MUST limit repair to missing repository scaffolding and refresh
of established generated-navigation files. It MUST NOT overwrite malformed
configuration, invent absent optional index files, mutate credentials, or
contact an external system to change state.
