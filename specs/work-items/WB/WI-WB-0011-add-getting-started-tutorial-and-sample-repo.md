---
artifact_id: WI-WB-0011
artifact_type: work_item
title: "add getting started tutorial and sample repo"
domain: WB
status: in_progress
owner: platform
addresses:
  - REQ-WB-RELEASE-0001
  - REQ-WB-RELEASE-0002
  - REQ-WB-RELEASE-0003
  - REQ-WB-RELEASE-0004
  - REQ-WB-RELEASE-0005
design_links:
  - ARC-WB-0007
verification_links:
  - VER-WB-0006
related_artifacts:
  - SPEC-WB-PUBLIC-RELEASE
  - ARC-WB-0007
  - VER-WB-0006
---

# WI-WB-0011 - add getting started tutorial and sample repo

Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

## Summary

Provide a step-by-step onboarding tutorial plus a small sample repository that
shows Workbench in action end-to-end.

## Requirements Addressed

- [`REQ-WB-RELEASE-0001`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0002`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0003`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0004`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0005`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)

## Design Inputs

- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

## Planned Changes

- A “Getting started” doc walks through install, init/scaffold, create a work
- item, and validate the repo.
- A sample repo (or scripted walkthrough in `examples/`) demonstrates the
- workflow with real work items and docs.
- README references the tutorial and sample so new users can find it quickly.

## Out of Scope

- Unrelated feature work.
- Changes to the linked requirement text.

## Verification Plan

State how the work will be proven and link the verification artifact.

## Completion Notes

Optional implementation notes, deviations, or follow-up items.

## Trace Links

Addresses:

- [`REQ-WB-RELEASE-0001`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0002`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0003`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0004`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0005`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)

Uses Design:

- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)

Verified By:

- [`VER-WB-0006`](../../verification/WB/VER-WB-0006-public-release-support.md)
