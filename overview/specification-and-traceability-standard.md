---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/overview/specification-and-traceability-standard.md"
  path: /overview/specification-and-traceability-standard.md
owner: platform
status: active
updated: 2026-03-20
---

# Specification and Traceability Standard

## Purpose

Workbench keeps the repository as the source of truth for product intent,
design, decisions, contracts, and delivery work. This standard explains how
those artifacts fit together without adding heavy requirements tooling.

The goal is practical traceability:

- requirements are explicit and easy to find
- architecture explains how requirements are satisfied
- work items show what will be built
- contracts and verification docs show how behavior is checked
- links stay local to the repository and survive file moves

## Scope

This standard applies to repository-native Markdown artifacts tracked by
Workbench:

- product requirement specs
- architecture/design docs
- decision records
- contracts and verification docs
- work items
- shared authoring templates

It does not require DOORS-style tooling, ReqIF authoring, or
language-specific test conventions.

## Artifact model

Workbench treats the repo as a linked set of artifact classes:

- `spec` documents describe requirements and expected behavior.
- `guide` documents describe architecture or design choices that satisfy
  requirements.
- `adr` documents capture decisions and tradeoffs.
- `contract` documents define schemas, interfaces, or verification contracts.
- `runbook` documents capture operational procedures.
- `doc` documents cover overviews, maps, and supporting prose.
- work items track delivery tasks, bugs, and spikes.

The important rule is not the label itself but the role:

- specs state what must be true
- architecture states how it will be made true
- work items state what will be done next
- contracts and verification docs state how it will be checked

## Folder layout

The repository now treats `overview/`, `contracts/`, `decisions/`,
`runbooks/`, and `tracking/` as the canonical homes for human-authored
documentation, `specs/` as the canonical home for requirement specifications,
`architecture/` as the canonical home for design docs, `templates/` as the
canonical home for reusable doc templates, `schemas/` as the canonical home
for JSON schemas, and `work/` as the canonical home for work items and
templates:

```text
overview/
  README.md
  *.md
contracts/
  README.md
  *.md
decisions/
  README.md
  *.md
runbooks/
  README.md
  *.md
tracking/
  README.md
  *.md
templates/
  README.md
  *.md
schemas/
  *.json
specs/
  README.md
  SPEC-<DOMAIN>[-<GROUPING>...].md
architecture/
  README.md
work/
  README.md
  items/
  done/
  templates/
```

Guidance:

- keep requirement specs directly under `specs/`
- keep architecture/design docs under `architecture`
- keep decision records under `decisions`
- keep contracts and verification docs under `contracts`
- keep work items under `work/items` and `work/done`
- keep shared authoring templates under `templates` and `work/templates`
- if a repository wants policy-driven IDs, define `artifact-id-policy.json`
  at the repo root and use `domain`/`capability` metadata in the spec or
  architecture front matter so Workbench can generate the matching ID
  automatically

## Requirement specs

Requirement specs are the explicit home for product requirements.

Rules:

- use Markdown as the authoring format
- keep specification front matter document-level and canonical
- include `artifact_id`, `domain`, and `capability` when the repository uses
  a policy-driven spec ID template
- keep each spec filename aligned with the full `SPEC-...` artifact ID
- make one file represent one coherent capability
- avoid mixing unrelated subsystems in the same spec
- avoid splitting a single capability into many tiny files
- keep requirement blocks easy for tooling to parse

Recommended structure:

```md
---
artifact_id: SPEC-CLI-ONBOARDING
artifact_type: specification
title: CLI Onboarding, Init Walkthrough, and Wizard Mode
domain: CLI
capability: onboarding
status: draft
owner: "<owner>"
---

# SPEC-CLI-ONBOARDING - CLI Onboarding, Init Walkthrough, and Wizard Mode

## Purpose

## Scope

## Context

## REQ-CLI-0001 Clarify default help output
The CLI MUST distinguish command groups from leaf commands in default help output.

Trace:
- Implemented By:
  - [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Verified By:
  - VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
- Related:
  - [ADR-2025-12-27-cli-onboarding-wizard](/decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

Notes:
- keep the output concise

## REQ-CLI-0002 Report repo health clearly
The CLI MUST provide a human-readable doctor summary with clear next steps.

Trace:
- Implemented By:
  - [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
- Verified By:
  - VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
- Related:
  - [ADR-2025-12-27-cli-onboarding-wizard](/decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

Notes:
- keep JSON as an opt-in machine format

```

Requirement guidance:

- use a stable `REQ-...` requirement ID per requirement block
- keep the normative clause immediately after the requirement heading
- use the approved all-caps keywords `MUST`, `MUST NOT`, `SHALL`,
  `SHALL NOT`, `SHOULD`, and `MAY`
- use `Trace` and `Notes` only when they add traceability value
- use `Satisfied By`, `Implemented By`, `Verified By`, `Test Refs`,
  `Code Refs`, and `Related` only when they add value
- keep the clause normative and verifiable
- when a requirement mentions another repository document, make it a clickable
  Markdown link or a repo-relative doc reference that Workbench can resolve

## Architecture docs

Architecture docs explain how requirements are satisfied by design.

Rules:

- keep architecture front matter document-level and canonical
- include `artifact_id` and `domain` when the repository uses a policy-driven
  architecture ID template
- describe the design, not the requirement itself
- point back to the spec blocks or requirement IDs being satisfied
- capture key components, constraints, data flow, and tradeoffs
- use the architecture template for a consistent shape

Recommended structure:

```md
---
artifact_id: ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
artifact_type: architecture
title: <Architecture or Design Title>
domain: <domain>
status: draft
owner: "<owner>"
satisfies:
  - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
related_artifacts:
  - SPEC-<DOMAIN>[-<GROUPING>...]
---

# ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> - <Architecture or Design Title>

## Purpose

## Requirements Satisfied
- REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

## Design summary

## Key components

## Data and state considerations

## Edge cases and constraints

## Alternatives considered
-

## Risks
-

## Related specs
- [CLI Onboarding, Init Walkthrough, and Wizard Mode](/specs/SPEC-CLI-ONBOARDING.md)

## Related work items
- [TASK-0001](/work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)

## Related ADRs
- [ADR-2025-12-27-cli-onboarding-wizard](/decisions/ADR-2025-12-27-cli-onboarding-wizard.md)

## Open questions
-
```

## Work items

Work items track delivery work and preserve the link to the spec or design
that motivated the change.

Rules:

- keep the work-item schema small and repo-native
- use the canonical body sections in the template
- keep title, summary, traceability, acceptance criteria, and notes editable
- preserve unknown sections when normalizing older files
- keep related specs and ADRs in front matter, not only in the body

Recommended body order:

1. Summary
2. Context
3. Traceability
4. Implementation notes
5. Acceptance criteria
6. Notes

## Traceability model

Traceability is intentionally lightweight:

- specs link to related architecture docs, work items, and ADRs
- architecture docs link to the requirements they satisfy and the work items
  that implement them
- work items link back to the specs, ADRs, files, PRs, and issues that define
  their scope
- contracts and verification docs point to the specs or architectures they
  support
- where the text cites another repository document, prefer clickable Markdown
  links in the body so the relationship survives plain reading and GitHub
  rendering

Workbench stores most of the cross-linking in front matter and keeps the body
sections readable for humans.

## Identifier conventions

- requirement IDs use a stable `REQ-<CODE>-0001` form
- work-item IDs remain `TASK-####`, `BUG-####`, or `SPIKE-####`
- ADRs keep their existing date-based naming convention
- file paths in links should remain repo-relative where possible
- use clickable Markdown links for repository documents whenever the target is
  known

The requirement code should be short and stable for the capability, such as
`CLI`, `WEB`, `QE`, `TUI`, or `SYNC`.

## Lifecycle expectations

Artifact lifecycle should be explicit but not over-modeled:

- specs usually move from draft to proposed, approved, implemented, verified,
  superseded, or retired
- architecture docs usually move from draft to active guidance and may later
  be superseded
- work items move through draft, ready, in-progress, blocked, done, or dropped
- contracts and runbooks may use their own status vocabulary when needed

Use status fields to communicate state, not to encode process ceremony.

## Authoring rules

- write the smallest artifact that still stands on its own
- prefer one capability per spec
- prefer one design problem per architecture doc
- avoid duplicating the same requirement across multiple docs
- preserve links when moving files so path history stays useful
- use TODOs or open questions when the source material is ambiguous
- do not invent product behavior that is not supported by the source material
- prefer clickable Markdown links over bare repo paths in authored bodies

## Tooling expectations

Workbench should support this model with repo-native commands:

- `workbench spec new/show/edit/delete/link/unlink/sync` should manage
  requirement specifications as the primary, specialized workflow
- A practical CLI workflow runbook lives at
  `runbooks/spec-cli-workflow.md`
- `workbench doc new/show/edit/delete/link/unlink/sync` should remain available
  for ADRs, guides, contracts, runbooks, and compatibility with older docs
- `workbench nav sync` should refresh docs and work indexes after file moves
- `workbench item normalize` should normalize work-item structure without
  deleting substantive content
- `workbench validate` should check schema, link, and location consistency

Generated views are advisory and reproducible. The authored Markdown files are
the source of truth.
