---
artifact_id: SPEC-CLI-ONBOARDING
artifact_type: specification
title: "CLI Onboarding, Init Walkthrough, and Wizard Mode"
domain: CLI
capability: onboarding
status: draft
owner: platform
related_artifacts:
  - TASK-0001
  - ADR-2025-12-27-cli-onboarding-wizard
workbench:
  type: spec
  workItems:
    - TASK-0001
  pathHistory: []
  path: /specs/requirements/CLI/SPEC-CLI-ONBOARDING.md
---

# SPEC-CLI-ONBOARDING - CLI Onboarding, Init Walkthrough, and Wizard Mode

## Purpose

Make first-run and ongoing CLI use clearer by tightening help output, turning
`init` into a guided setup flow, and adding a wizard-like command for common
actions.

## Scope

- improve default help output and version reporting
- add a guided `doctor` and `init` experience for new or partially scaffolded repos
- add a wizard or guide command for common document and work-item actions
- keep the implementation repo-native and compatible with the current CLI

## Context

The existing CLI already exposes the core commands, but a new user still has to
guess which commands are groups, which ones are leaf actions, and what to do on
first run. The current `init` flow is too easy to skip past, and the repo needs
an obvious guided path for people who do not yet know the command surface.

## REQ-CLI-0001 Clarify default help output
The CLI MUST distinguish command groups from leaf commands in default help output and avoid duplicate or confusing version commands or flags.

Trace:
- Implemented By:
  - [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Related:
  - [ADR-2025-12-27-cli-onboarding-wizard](/docs/40-decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

Notes:
- keep the output concise
- populate version values from build or assembly metadata

## REQ-CLI-0002 Report repo health clearly
The CLI MUST provide a human-readable doctor summary with clear next steps and only emit JSON when explicitly requested.

Trace:
- Implemented By:
  - [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Related:
  - [ADR-2025-12-27-cli-onboarding-wizard](/docs/40-decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

Notes:
- include guidance for resolving failed checks
- keep JSON as an opt-in machine format

## REQ-CLI-0003 Guide first-run setup
The CLI MUST run a guided `init` flow that helps the user complete setup even when some folders or files already exist.

Trace:
- Implemented By:
  - [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Related:
  - [ADR-2025-12-27-cli-onboarding-wizard](/docs/40-decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

Notes:
- prompt for OpenAI settings when needed
- prompt for credential storage location
- support a non-interactive mode for CI use
- offer a way to skip the wizard after init

## REQ-CLI-0004 Support common repo actions
The CLI MUST provide a wizard or guide command that helps the user choose the next common action, select templates, and create or inspect docs and work items.

Trace:
- Implemented By:
  - [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Related:
  - [ADR-2025-12-27-cli-onboarding-wizard](/docs/40-decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

Notes:
- offer descriptions of each doc and work-item type
- guide the user through required fields
- exit back to standard commands with a short next-steps message

## REQ-CLI-0005 Lead first run into setup
The CLI MUST detect first run from the absence of the Workbench config or workspace state and launch the guided setup flow, then the wizard unless the user explicitly skips it.

Trace:
- Implemented By:
  - [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Related:
  - [ADR-2025-12-27-cli-onboarding-wizard](/docs/40-decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

Notes:
- define first run as missing `.workbench/` or `.workbench/config`
- preserve normal help behavior after the repository is initialized
