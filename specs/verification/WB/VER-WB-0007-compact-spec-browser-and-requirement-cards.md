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
  - REQ-WEB-0014
  - REQ-WEB-0015
  - REQ-WEB-0016
related_artifacts:
  - SPEC-WEB-LOCAL-UI
  - ARC-WB-0006
  - WI-WB-0023
---

# VER-WB-0007 - Compact Spec Browser and Requirement Cards

## Scope

Compact specs list layout, identifier-family grouping, requirement-card editing
controls, Save All validation, optional section toggles, and hidden preview
behavior.

## Requirements Verified

- [`REQ-WEB-0006`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0007`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0008`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0009`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0010`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0011`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0012`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0013`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0014`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0015`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0016`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)

## Verification Method

Manual browser inspection plus targeted UI tests.

## Preconditions

- The local web UI slice is implemented.
- The Specs page includes the compact list and requirement-card editor.

## Procedure or Approach

1. Open the Specs page in the local web UI.
2. Confirm the list is grouped by `SPEC-<DOMAIN>` families.
3. Confirm each card shows only the spec ID and title in the default view.
4. Click Add Requirement and confirm a blank requirement card appears with the ID, title, and clause inputs visible.
5. Confirm the card exposes Save, Delete, Move Earlier, and Move Later controls.
6. Click Delete on a requirement card and confirm the card is removed from the editing list.
7. Click Move Earlier and Move Later on a requirement card and confirm the order changes in the list.
8. Click Save on a requirement card with valid data and confirm only that card is persisted.
9. Click Save on a requirement card with an invalid clause keyword and confirm the card remains unsaved with visible validation errors.
10. Create one valid card and one invalid card, click Save All, and confirm nothing is persisted and the invalid card remains visibly flagged.
11. Click Edit on the Core Narrative, Open Questions, and Related Artifacts sections and confirm saved content is preserved.
12. Confirm the rendered Markdown preview starts hidden and only appears after the user expands the toggle.

## Expected Result

The Specs page uses the compact grouped layout, the requirement editor exposes the
required fields and card actions, Save All is all-or-nothing, invalid clauses are
rejected before a file is written, and the preview remains hidden by default.

## Evidence

- <browser walkthrough notes>
- <page-level UI tests>

## Status

The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.

planned

## Related Artifacts

- [`SPEC-WEB-LOCAL-UI`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`ARC-WB-0006`](../../architecture/WB/ARC-WB-0006-local-web-ui-mode.md)
- [`WI-WB-0023`](../../work-items/WB/WI-WB-0023-build-local-web-ui-mode-for-workbench.md)
