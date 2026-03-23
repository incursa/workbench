---
artifact_id: ARC-WB-0004
artifact_type: architecture
title: "Repo-native operating model"
domain: WB
status: approved
owner: platform
satisfies:
  - REQ-WB-STD-0001
  - REQ-WB-STD-0002
  - REQ-WB-STD-0003
  - REQ-WB-STD-0004
  - REQ-CLI-SURFACE-0001
  - REQ-CLI-SURFACE-0002
  - REQ-CLI-SURFACE-0003
  - REQ-CLI-SURFACE-0004
  - REQ-CLI-SURFACE-0005
  - REQ-CLI-SURFACE-0006
  - REQ-CLI-ITEM-0001
  - REQ-CLI-ITEM-0002
  - REQ-CLI-ITEM-0003
  - REQ-CLI-ITEM-0004
  - REQ-CLI-ITEM-0005
  - REQ-CLI-ITEM-0006
  - REQ-CLI-ITEM-0007
  - REQ-CLI-DOC-0001
  - REQ-CLI-DOC-0002
  - REQ-CLI-DOC-0003
  - REQ-CLI-DOC-0004
  - REQ-CLI-DOC-0005
  - REQ-CLI-DOC-0006
  - REQ-CLI-DOC-0007
  - REQ-CLI-NAV-0001
  - REQ-CLI-NAV-0002
  - REQ-CLI-NAV-0003
  - REQ-CLI-NAV-0004
  - REQ-CLI-NAV-0005
  - REQ-CLI-NAV-0006
  - REQ-CLI-MIGRATE-0001
  - REQ-CLI-MIGRATE-0002
  - REQ-CLI-MIGRATE-0003
  - REQ-CLI-MIGRATE-0004
  - REQ-CLI-MIGRATE-0005
  - REQ-CLI-SCAFFOLD-0001
  - REQ-CLI-SCAFFOLD-0002
  - REQ-CLI-SCAFFOLD-0003
  - REQ-CLI-SCAFFOLD-0004
  - REQ-CLI-SCAFFOLD-0005
  - REQ-SYNC-0001
  - REQ-SYNC-0002
  - REQ-SYNC-0003
  - REQ-SYNC-0004
  - REQ-SYNC-0005
related_artifacts:
  - SPEC-WB-STD
  - SPEC-SYNC-WORK-ITEM-SYNC
  - SPEC-CLI-SURFACE
  - SPEC-CLI-ITEM
  - SPEC-CLI-DOC
  - SPEC-CLI-NAV
  - SPEC-CLI-MIGRATE
  - SPEC-CLI-SCAFFOLD
  - VER-WB-0001
  - VER-WB-0005
---

# ARC-WB-0004 - Repo-native operating model

## Purpose

Workbench already has a strong local data model: Markdown docs and work items,
repo config, generated indexes, validation, optional GitHub sync, and explicit
command/output contracts. What is missing is a single operating model that
explains which artifacts are canonical, which ones are derived, and who the
product is optimized for.

Without that boundary, humans can mistake generated boards for hand-authored
state, and agents can over-rotate toward GitHub or helper docs instead of the
repo-native Markdown record.

## Requirements Satisfied

- REQ-WB-STD-0001
- REQ-WB-STD-0002
- REQ-WB-STD-0003
- REQ-WB-STD-0004
- REQ-CLI-SURFACE-0001
- REQ-CLI-SURFACE-0002
- REQ-CLI-SURFACE-0003
- REQ-CLI-SURFACE-0004
- REQ-CLI-SURFACE-0005
- REQ-CLI-SURFACE-0006
- REQ-CLI-ITEM-0001
- REQ-CLI-ITEM-0002
- REQ-CLI-ITEM-0003
- REQ-CLI-ITEM-0004
- REQ-CLI-ITEM-0005
- REQ-CLI-ITEM-0006
- REQ-CLI-ITEM-0007
- REQ-CLI-DOC-0001
- REQ-CLI-DOC-0002
- REQ-CLI-DOC-0003
- REQ-CLI-DOC-0004
- REQ-CLI-DOC-0005
- REQ-CLI-DOC-0006
- REQ-CLI-DOC-0007
- REQ-CLI-NAV-0001
- REQ-CLI-NAV-0002
- REQ-CLI-NAV-0003
- REQ-CLI-NAV-0004
- REQ-CLI-NAV-0005
- REQ-CLI-NAV-0006
- REQ-CLI-MIGRATE-0001
- REQ-CLI-MIGRATE-0002
- REQ-CLI-MIGRATE-0003
- REQ-CLI-MIGRATE-0004
- REQ-CLI-MIGRATE-0005
- REQ-CLI-SCAFFOLD-0001
- REQ-CLI-SCAFFOLD-0002
- REQ-CLI-SCAFFOLD-0003
- REQ-CLI-SCAFFOLD-0004
- REQ-CLI-SCAFFOLD-0005
- REQ-SYNC-0001
- REQ-SYNC-0002
- REQ-SYNC-0003
- REQ-SYNC-0004
- REQ-SYNC-0005

## Design Summary

Workbench is an AI-first, human-auditable, repo-native work system.

The primary user is an engineer working in a git repository with an AI agent.
The secondary user is a human reviewer or maintainer who needs to understand
the plan, execution state, and historical rationale from the repo alone.

Workbench exists to keep planning, execution context, validation, and proof of
work in version-controlled Markdown with predictable command contracts. It is
not a hosted project-management system.

Canonical artifacts:

- `.workbench/config.json` and the schemas under `schemas/`
- Markdown docs under `runbooks/`, `tracking/`, `specs/requirements/`, `specs/architecture/`, `specs/work-items/`, `specs/verification/`, `specs/generated/`, `specs/templates/`, and `specs/schemas/`
- Work item Markdown files under `specs/work-items/`
- The front matter and body content inside those files

Derived artifacts:

- Marker-bounded sections in repo READMEs and indexes
- Backlinks, normalized front matter, and change-note summaries
- GitHub issues/branches/PR backlinks created from local work items

Human-authored content:

- Specs, runbooks, guides, and narrative docs
- Work item titles, summaries, acceptance criteria, notes, and final status
- Decisions about scope, tradeoffs, and what links are meaningful

Agent-generated or agent-maintained content:

- First-draft work items or docs from prompts, voice capture, or diffs
- Front matter normalization and backlink repair
- Generated indexes, summaries, and sync reports
- Optional GitHub issue/PR creation that mirrors local state

GitHub relationship:

- GitHub issues are optional intake or mirror records for local work items.
- Branches and pull requests are execution artifacts linked back to work items.
- GitHub sync must not displace local Markdown as the system of record.
- When conflicts exist, Workbench should require an explicit choice or use a
  configured default, but the local repo model remains the conceptual source of
  truth.

Out of scope:

- Turning Workbench into Jira-in-Markdown
- Portfolio planning, capacity management, sprint boards, or SaaS-style
  dashboards
- Multi-user workflow state outside the git repository
- Treating GitHub Issues as the primary canonical record

## Key Components

- Canonical Markdown artifacts as the repository system of record
- Derived indexes and backlinks as generated views
- Shared services for validation, sync, and navigation
- GitHub as an optional mirror/intake surface

## Data and State Considerations

The system must keep authored state distinct from generated state and preserve stable links as files move.

## Edge Cases and Constraints

- Generated indexes should never be mistaken for authored truth.
- Local files remain the conceptual source of truth even when GitHub sync is enabled.
- Validation should prefer explicit links and stable IDs over prose references.

## Alternatives Considered

- GitHub-first workflow with local files as exports: rejected because it weakens
  repo auditability and makes agent workflows less deterministic.
- Full project-management surface inside Workbench: rejected because it would
  add ceremony and broaden the product beyond repo-native execution.
- Agent-only workflow with opaque generated state: rejected because humans still
  need to review, edit, and trust the repository record.

## Risks

- Human-facing docs and commands should point users toward creating and editing
  canonical local files first, then refreshing derived views.
- Generated sections should be clearly marked as generated and easy to rebuild.
- Agent affordances should stay predictable: stable schemas, JSON output, and
  low-ambiguity command behavior.
- Some duplication remains acceptable when it improves auditability, but it
  should be minimized where it creates confusion rather than clarity.

## Open Questions

- None.
