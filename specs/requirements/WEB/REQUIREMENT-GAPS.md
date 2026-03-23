# WEB Requirement Gaps

This file tracks unresolved decisions from the first pass of the local web UI
spec browser and requirement editor requirements.

Primary spec: `SPEC-WEB-LOCAL-UI`.

## Open Gaps

### GAP-WEB-0001 - Identifier-family grouping source

Affected requirements: REQ-WEB-0007, VER-WB-0007

Open question: should the Specs page always group by the stable `SPEC-<DOMAIN>`
prefix, or should it follow a configurable grouping registry when one exists?

Why it matters: the grouping rule affects how specs are listed and how future
identifier policy changes are surfaced in the UI.

### GAP-WEB-0002 - Requirement-card lifecycle

Affected requirements: REQ-WEB-0010, REQ-WEB-0011, VER-WB-0007

Open question: should the first pass support only append-and-edit behavior, or
should requirement cards also support delete and reordering actions?

Why it matters: the UI shape and the save workflow depend on whether cards are
source-order only or can be rearranged interactively.

### GAP-WEB-0003 - Validation failure behavior

Affected requirements: REQ-WEB-0012, VER-WB-0007

Open question: when a requirement clause fails validation, should the page block
the whole spec save, mark the specific card inline, or allow a draft save with
warnings?

Why it matters: the user experience for invalid requirements changes depending
on whether save is hard-blocked or advisory.

### GAP-WEB-0004 - Empty optional sections

Affected requirements: REQ-WEB-0013, VER-WB-0007

Open question: should empty Core Narrative, Open Questions, and Related
Artifacts sections be hidden entirely, or shown as collapsed placeholders with
Edit buttons?

Why it matters: this affects the default page density and how much of the page
is visible before the user starts editing.

### GAP-WEB-0005 - Rendered preview retention

Affected requirements: REQ-WEB-0003

Open question: should the Specs page keep the rendered Markdown preview, hide it
behind a toggle, or remove it from the page for the first pass?

Why it matters: the preview is useful for review, but it adds vertical space and
competes with the compact editing layout.

## Assumptions In This Pass

- Specs are grouped by the stable `SPEC-<DOMAIN>` prefix.
- The Add Requirement button appends a new blank card to the end of the list.
- Save-time validation rejects malformed clauses before the file is written.
- Optional narrative sections preserve saved values when they are collapsed and
  reopened.

## Follow-Up Questions

- Do you want requirement cards to support delete and reordering in this pass?
- Should invalid requirement clauses block the entire save, or only the card
  that failed validation?
- Should the rendered preview stay visible on the Specs page?
