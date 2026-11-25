---
artifact_id: WI-WB-0030
artifact_type: work_item
title: Qualify SpecAttest Standard 0.1.0 for stable release
domain: WB
status: in_progress
owner: platform
addresses:
  - REQ-WB-PROVIDER-0010
design_links:
  - ARC-WB-0010
verification_links:
  - VER-WB-0013
related_artifacts:
  - SPEC-WB-RESOURCE-PROVIDERS
  - VER-WB-0012
priority: high
---

# WI-WB-0030 - Qualify SpecAttest Standard 0.1.0 for stable release

## Summary

Run the candidate soak and cross-tool qualification needed to decide whether
the exact SpecAttest Standard `0.1.0-rc.1` contract can be promoted without
normative change to stable `0.1.0`.

## Requirements Addressed

- [`REQ-WB-PROVIDER-0010`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

The cross-repository Standard requirements evaluated by this work are
`REQ-VER-0005`, `REQ-VER-0009`, `REQ-VER-0011`, `REQ-VER-0012`,
`REQ-VER-0014`, `REQ-VER-0018`, `REQ-VER-0019`, and `REQ-VER-0021`.

## Planned Changes

- Retain a fresh RC1 qualification baseline from the frozen release assets,
  independent Workbench validation, the Manager protocol harness, and the live
  LXC deployment.
- Observe a seven-day candidate soak from 2026-11-24T21:10:39Z through
  2026-12-01T21:10:39Z.
- Review incidents, issues, and behavioral differences at the decision point.
- If the normative contract remains unchanged, prepare the immutable stable
  manifest, signed tag, non-prerelease release, independent redownload
  verification, and downstream pin updates.
- If any normative or validation-behavior change is needed, stop stable
  promotion and qualify `0.1.0-rc.2` instead.

## Out of Scope

- Claiming external standards-body ratification.
- Publishing `0.1.0` before the soak and explicit owner approval are complete.
- Treating the Manager package build number as the Standard edition.

## Verification Plan

Follow [VER-WB-0013](../../verification/WB/VER-WB-0013-specattest-0.1.0-stable-qualification.md).
The verification remains planned while the time-bound soak gate is open.

## Completion Notes

The 2026-11-25 entry baseline passed. A stale draft.2 label in the Manager's
reproducible protocol harness was found and corrected by retaining a new
RC1-specific evidence matrix; the underlying five vectors and ten transport
outcomes remained unchanged and passed.

## Trace Links

Addresses:

- [`SPEC-WB-RESOURCE-PROVIDERS`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

Uses Design:

- [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)

Verified By:

- `VER-WB-0013`
