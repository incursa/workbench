---
artifact_id: SPEC-CLI-INIT
artifact_type: specification
title: "CLI Init Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-ONBOARDING
  - TASK-0001
---

# SPEC-CLI-INIT - CLI Init Command

## Purpose

Define the contract for first-run repository setup.

## Scope

- `workbench init`

## REQ-CLI-INIT-0001 `workbench init`

`init` MUST run the guided setup flow, accept the documented scaffold,
OpenAI, credential, and skip-guide options, and handle partially initialized
repos without requiring a clean slate.

## REQ-CLI-INIT-0002 Non-interactive setup

`init` MUST support non-interactive execution for CI and scripted bootstrap
scenarios and fails clearly when required flags are missing in that mode.

## REQ-CLI-INIT-0003 Follow-up guidance

`init` MUST launch or skip the guide based on `--skip-guide`.

## REQ-CLI-INIT-0004 Ready state

`init` MUST leave the repository in a state where the user can immediately
continue with the next command.

## REQ-CLI-INIT-0005 Existing-state preservation

`init` MUST preserve existing repo config and credential data that it does not
need to rewrite.

## REQ-CLI-INIT-0006 Ready-state reporting

`init` MUST report the resulting setup state so the caller can continue with
the next command.
