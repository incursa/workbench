# WEB Requirement Gaps

This file tracks unresolved decisions for the local web UI spec browser and requirement editor.

Primary spec: [`SPEC-WEB-LOCAL-UI`](SPEC-WEB-LOCAL-UI.md).

## Open Gaps

### GAP-WEB-0001 - Identifier-family grouping source

Affected requirements: [`REQ-WEB-0007`](SPEC-WEB-LOCAL-UI.md), [`VER-WB-0007`](../../verification/WB/VER-WB-0007-compact-spec-browser-and-requirement-cards.md)

Open question: should the Specs page always group by the stable `SPEC-<DOMAIN>`
prefix, or should it follow a configurable grouping registry when one exists?

Why it matters: the grouping rule affects how specs are listed and how future
identifier policy changes are surfaced in the UI.

## Resolved Decisions

- Requirement cards expose Save, Delete, and Move Earlier/Move Later controls.
- Individual card Save validates and persists only the targeted card.
- Save All is all-or-nothing and must not partially persist when any card is invalid.
- Empty Core Narrative, Open Questions, and Related Artifacts sections start collapsed and read-only when empty, then expand via Edit.
- The rendered Markdown preview starts hidden behind a collapsed toggle.

## Follow-Up Questions

- Should identifier-family grouping always use the `SPEC-<DOMAIN>` prefix, or should it follow a configurable registry when one exists?
