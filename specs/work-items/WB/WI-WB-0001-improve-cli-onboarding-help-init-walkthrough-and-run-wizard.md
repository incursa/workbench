---
artifact_id: WI-WB-0001
artifact_type: work_item
title: "Improve CLI onboarding help, init walkthrough, and run wizard"
domain: WB
status: planned
owner: platform
addresses:
  - REQ-CLI-0001
  - REQ-CLI-0002
  - REQ-CLI-0003
  - REQ-CLI-0004
  - REQ-CLI-0005
design_links:
  - ARC-WB-0001
  - ARC-WB-0007
verification_links:
  - VER-WB-0001
related_artifacts:
  - SPEC-CLI-ONBOARDING
  - ARC-WB-0001
  - ARC-WB-0007
  - VER-WB-0001
---

# WI-WB-0001 - Improve CLI onboarding help, init walkthrough, and run wizard

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Deliver clearer CLI onboarding by updating help output, adding a guided `init`,
and introducing a guided command for common repo actions, per the linked
feature spec.

## Requirements Addressed

- REQ-CLI-0001
- REQ-CLI-0002
- REQ-CLI-0003
- REQ-CLI-0004
- REQ-CLI-0005

## Design Inputs

- ARC-WB-0001
- ARC-WB-0007

## Planned Changes

- Help output distinguishes command groups vs. leaf commands and version
- reporting is not duplicated or missing.
- `init` runs interactively with explicit steps, including guidance for
- front matter and OpenAI configuration, with non-interactive flags available.
- `init` prompts for credential storage (outside repo or ignored local file)
- and ensures any local file path is added to `.gitignore`.
- `guide` provides guided selection of common document/work item types with
- clear descriptions and next steps.
- First run is detected when `.workbench/config` (or `.workbench/`) is missing.
- First run automatically executes `init` and then launches `guide` unless
- `--skip-guide` is set.
- `doctor` defaults to human-readable output with `--json` required for machine
- output.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- REQ-CLI-0001
- REQ-CLI-0002
- REQ-CLI-0003
- REQ-CLI-0004
- REQ-CLI-0005

Uses Design:

- ARC-WB-0001
- ARC-WB-0007

Verified By:

- VER-WB-0001
