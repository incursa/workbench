---
artifact_id: WI-WB-9001
artifact_type: work_item
title: "Contract Fixture"
domain: WB
status: planned
owner: platform
addresses:
  - REQ-CLI-0001
design_links:
  - ARC-WB-0001
verification_links:
  - VER-WB-0001
related_artifacts:
  - SPEC-CLI-ONBOARDING
  - ARC-WB-0001
  - VER-WB-0001
---

# WI-WB-9001 - Contract Fixture

Use one of the approved work-item statuses: `planned`, `in_progress`,
`blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Valid work item fixture for parser and schema tests.

## Requirements Addressed

- [`REQ-CLI-0001`](../../specs/requirements/CLI/SPEC-CLI-ONBOARDING.md)

## Design Inputs

- [`ARC-WB-0001`](../../specs/architecture/WB/ARC-WB-0001-cli-onboarding-flow-with-init-run-wizard.md)

## Planned Changes

No implementation changes are described here because this file is only a
contract fixture.

## Out of Scope

- Behavior changes.

## Verification Plan

Validate the front matter against the canonical work-item schema.

## Completion Notes

This fixture is intentionally minimal while still using canonical trace fields.

## Trace Links

Addresses:

- [`REQ-CLI-0001`](../../specs/requirements/CLI/SPEC-CLI-ONBOARDING.md)

Uses Design:

- [`ARC-WB-0001`](../../specs/architecture/WB/ARC-WB-0001-cli-onboarding-flow-with-init-run-wizard.md)

Verified By:

- [`VER-WB-0001`](../../specs/verification/WB/VER-WB-0001-repo-operations-and-command-surface.md)
