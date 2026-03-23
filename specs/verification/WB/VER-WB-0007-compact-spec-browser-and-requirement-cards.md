---
artifact_id: VER-WB-0007
artifact_type: verification
title: "Compact Spec Browser and Requirement Cards"
domain: WB
status: planned
owner: platform
verifies:
  - REQ-WEB-0006
  - REQ-WEB-0007
  - REQ-WEB-0008
  - REQ-WEB-0009
  - REQ-WEB-0010
  - REQ-WEB-0011
  - REQ-WEB-0012
  - REQ-WEB-0013
related_artifacts:
  - SPEC-WEB-LOCAL-UI
  - ARC-WB-0006
  - WI-WB-0023
---

# VER-WB-0007 - Compact Spec Browser and Requirement Cards

## Scope

Compact specs list layout, identifier-family grouping, requirement-card editing
controls, optional section toggles, and save-time requirement validation.

## Requirements Verified

- REQ-WEB-0006
- REQ-WEB-0007
- REQ-WEB-0008
- REQ-WEB-0009
- REQ-WEB-0010
- REQ-WEB-0011
- REQ-WEB-0012
- REQ-WEB-0013

## Verification Method

Manual browser inspection plus targeted UI tests.

## Preconditions

- The local web UI slice is implemented.
- The Specs page includes the compact list and requirement-card editor.

## Procedure or Approach

1. Open the Specs page in the local web UI.
2. Confirm the list is grouped by `SPEC-<DOMAIN>` families.
3. Confirm each card shows only the spec ID and title in the default view.
4. Add a requirement card and confirm the ID, title, and clause inputs are visible.
5. Save a requirement with an invalid clause keyword and confirm the save is rejected.
6. Expand the Core Narrative, Open Questions, and Related Artifacts sections and confirm saved content is preserved.

## Expected Result

The Specs page uses the compact grouped layout, the requirement editor exposes the
required fields, and invalid clauses are rejected before a file is written.

## Evidence

- <browser walkthrough notes>
- <page-level UI tests>

## Status

The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.

planned

## Related Artifacts

- SPEC-WEB-LOCAL-UI
- ARC-WB-0006
- WI-WB-0023
