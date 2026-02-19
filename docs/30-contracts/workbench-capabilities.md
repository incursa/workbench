---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/30-contracts/workbench-capabilities.md
owner: platform
status: active
updated: 2025-12-29
---

# Workbench Capabilities Map (v0.1)

This document maps user-facing capabilities to Workbench CLI commands and highlights known gaps.
It is intentionally capability-first rather than a full command catalog.

## Setup and diagnostics

- Initialize repo scaffolding, config, and onboarding flow -> `workbench init` (or just scaffolding via `workbench scaffold`)
- Run an interactive wizard for common actions -> `workbench run`
- Inspect repo health, git, and provider readiness -> `workbench doctor`
- Show CLI version -> `workbench version`
- Inspect effective configuration -> `workbench config show`
- Update config values -> `workbench config set`
- Write/update credentials.env entries -> `workbench config credentials set`, `workbench config credentials unset`

## Work items

- Create a work item from structured input -> `workbench item new`
- Generate a work item from a prompt (AI) -> `workbench item generate`
- Import a GitHub issue into a work item -> `workbench item import`
- Sync work items with GitHub issues/branches (two-way, no deletes) -> `workbench item sync` (or `workbench sync --items`)
- List or inspect work items -> `workbench item list`, `workbench item show`
- Update status and append a note -> `workbench item status`
- Close an item and optionally move it to `docs/70-work/done` -> `workbench item close`
- Delete work items and remove doc backlinks -> `workbench item delete`
- Move or rename items and update inbound links -> `workbench item move`, `workbench item rename`
- Normalize work item front matter lists -> `workbench item normalize`
- Link/unlink specs, ADRs, files, PRs, and issues -> `workbench item link`, `workbench item unlink`
- Regenerate the workboard -> `workbench board regen`

## Docs and knowledge base

- Create docs/specs/ADRs/runbooks/guides with front matter/backlinks -> `workbench doc new`
- Delete docs and remove work item links -> `workbench doc delete`
- Link/unlink docs to work items -> `workbench doc link`, `workbench doc unlink`
- Sync doc front matter and backlinks -> `workbench doc sync` (or `workbench sync --docs`)
- Summarize doc changes into change notes (AI) -> `workbench doc summarize`
- Regenerate navigation indexes and optionally workboard -> `workbench nav sync` (or `workbench sync --nav`)

## GitHub and promotion flows

- Create a PR from a work item -> `workbench github pr create`
- Promote a task into a branch/commit/PR flow -> `workbench promote`
- Run full repo sync (items + docs + nav) -> `workbench sync`

## Validation

- Validate schemas, links, and IDs -> `workbench validate` (alias: `workbench verify`)

## Gaps and missing capabilities

- No CLI command to edit work item content/body or arbitrary fields beyond status/title/links.
- No CLI command to create or close GitHub issues directly (import/sync only).
- No CLI command to delete or close GitHub PRs (linking only).
- No bulk operations for status updates, relinking, or mass edits.
