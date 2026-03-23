---
artifact_id: SPEC-CLI-ONBOARDING
artifact_type: specification
title: "CLI Onboarding and First-Run Flow"
domain: CLI
capability: onboarding
status: draft
owner: platform
related_artifacts:
  - WI-WB-0001
  - SPEC-CLI-SURFACE
  - SPEC-CLI-DOCTOR
  - SPEC-CLI-GUIDE
  - SPEC-CLI-INIT
  - ARC-WB-0001
---

# SPEC-CLI-ONBOARDING - CLI Onboarding and First-Run Flow

## Purpose

Make first-run and ongoing CLI use clearer by tightening help output, turning
`init` into a guided setup flow, and adding a guided command for common
actions.

## Scope

- improve default help output and version reporting
- provide a clear first-run path into the dedicated `doctor`, `init`, and
  `guide` commands
- keep the implementation repo-native and compatible with the current CLI

## Context

The existing CLI already exposes the core commands, but a new user still has to
guess which commands are groups, which ones are leaf actions, and what to do on
first run. The current `init` flow is too easy to skip past, and the repo needs
an obvious guided path for people who do not yet know the command surface.

## REQ-CLI-0001 Clarify default help output
The CLI MUST distinguish command groups from leaf commands in default help
output and avoid duplicate or confusing version commands or flags.

Trace:
- Implemented By:
  - [WI-WB-0001](/specs/work-items/WB/WI-WB-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Related:
  - [ARC-WB-0001](/architecture/ARC-WB-0001-cli-onboarding-flow-with-init-run-wizard.md)

Notes:
- keep the output concise
- populate version values from build or assembly metadata

## REQ-CLI-0002 Provide clear first-run routing
The CLI MUST provide a clear first-run path that routes users into the
dedicated `doctor`, `init`, and `guide` commands without requiring them to
discover the command tree manually.

Trace:
- Implemented By:
  - [WI-WB-0001](/specs/work-items/WB/WI-WB-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Related:
  - [ARC-WB-0001](/architecture/ARC-WB-0001-cli-onboarding-flow-with-init-run-wizard.md)

Notes:
- preserve normal help behavior after the repository is initialized
- keep first-run guidance aligned with the live command tree

## REQ-CLI-0003 Guided entrypoints

`guide` and `init` MUST be the explicit first-run entrypoints for user
onboarding.

## REQ-CLI-0004 Post-init stability

After initialization, the CLI MUST preserve standard help behavior and stop
forcing onboarding routes.

## REQ-CLI-0005 Entry routing

The onboarding flow MUST route users through `doctor`, `init`, and `guide`
without introducing extra entrypoints.
