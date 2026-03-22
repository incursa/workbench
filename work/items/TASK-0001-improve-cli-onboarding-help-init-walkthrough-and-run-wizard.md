---
id: TASK-0001
type: task
status: draft
priority: high
owner: platform
created: 2025-12-27
updated: null
tags: []
related:
  specs:
    - /specs/SPEC-CLI-ONBOARDING.md
  adrs:
    - /decisions/ADR-2025-12-27-cli-onboarding-wizard.md
  files:
    - /specs/SPEC-CLI-ONBOARDING.md
    - /decisions/ADR-2025-12-27-cli-onboarding-wizard.md
  prs: []
  issues:
    - "https://github.com/incursa/workbench/issues/15"
  branches:
    - TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard
title: "Improve CLI onboarding help, init walkthrough, and run wizard"
githubSynced: null
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md
---

# TASK-0001 - Improve CLI onboarding help, init walkthrough, and run wizard

## Summary

Deliver clearer CLI onboarding by updating help output, adding a guided `init`,
and introducing a guided command for common repo actions, per the linked
feature spec.

## Context

-

## Traceability

- Requirement IDs: []
- Architecture docs: []
- Verification docs: []
- Related contracts or runbooks: []

## Implementation notes

-

## Acceptance criteria

- Help output distinguishes command groups vs. leaf commands and version
  reporting is not duplicated or missing.
- `init` runs interactively with explicit steps, including guidance for
  front matter and OpenAI configuration, with non-interactive flags available.
- `init` prompts for credential storage (outside repo or ignored local file)
  and ensures any local file path is added to `.gitignore`.
- `guide` provides guided selection of common document/work item types with
  clear descriptions and next steps.
- First run is detected when `.workbench/config` (or `.workbench/`) is missing.
- First run automatically executes `init` and then launches `guide` unless
  `--skip-guide` is set.
- `doctor` defaults to human-readable output with `--json` required for machine
  output.

## Notes

-
