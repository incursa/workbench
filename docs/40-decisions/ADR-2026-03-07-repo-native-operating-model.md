---
workbench:
  type: adr
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md"
  path: /docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md
owner: platform
status: accepted
updated: 2026-03-07
---

# ADR-2026-03-07: Repo-native operating model

- Status: accepted
- Date: 2026-03-07
- Owner: platform

## Context

Workbench already has a strong local data model: Markdown docs and work items,
repo config, generated indexes, validation, optional GitHub sync, and explicit
command/output contracts. What is missing is a single operating model that
explains which artifacts are canonical, which ones are derived, and who the
product is optimized for.

Without that boundary, humans can mistake generated boards for hand-authored
state, and agents can over-rotate toward GitHub or helper docs instead of the
repo-native Markdown record.

## Decision

Workbench is an AI-first, human-auditable, repo-native work system.

The primary user is an engineer working in a git repository with an AI agent.
The secondary user is a human reviewer or maintainer who needs to understand
the plan, execution state, and historical rationale from the repo alone.

Workbench exists to keep planning, execution context, validation, and proof of
work in version-controlled Markdown with predictable command contracts. It is
not a hosted project-management system.

Canonical artifacts:

- `.workbench/config.json` and the schemas/contracts under `docs/30-contracts/`
- Markdown docs under `docs/`
- Work item Markdown files under `docs/70-work/items/` and `docs/70-work/done/`
- The front matter and body content inside those files

Derived artifacts:

- Marker-bounded sections in repo READMEs and indexes
- Backlinks, normalized front matter, and change-note summaries
- GitHub issues/branches/PR backlinks created from local work items

Human-authored content:

- Specs, ADRs, runbooks, guides, and narrative docs
- Work item titles, summaries, acceptance criteria, notes, and final status
- Decisions about scope, tradeoffs, and what links are meaningful

Agent-generated or agent-maintained content:

- First-draft work items or docs from prompts, voice capture, or diffs
- Front matter normalization and backlink repair
- Generated indexes, boards, summaries, and sync reports
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

## Alternatives considered

- GitHub-first workflow with local files as exports: rejected because it weakens
  repo auditability and makes agent workflows less deterministic.
- Full project-management surface inside Workbench: rejected because it would
  add ceremony and broaden the product beyond repo-native execution.
- Agent-only workflow with opaque generated state: rejected because humans still
  need to review, edit, and trust the repository record.

## Consequences

- Human-facing docs and commands should point users toward creating and editing
  canonical local files first, then refreshing derived views.
- Generated sections should be clearly marked as generated and easy to rebuild.
- Agent affordances should stay predictable: stable schemas, JSON output, and
  low-ambiguity command behavior.
- Some duplication remains acceptable when it improves auditability, but it
  should be minimized where it creates confusion rather than clarity.

## Related specs

- `/docs/00-overview/workbench-spec.md`
- `/docs/10-product/specs/feature-spec-work-item-sync.md`

## Related work items

- None.
