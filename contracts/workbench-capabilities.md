---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/contracts/workbench-capabilities.md"
  path: /contracts/workbench-capabilities.md
owner: platform
status: active
updated: 2026-03-20
---

# Workbench Capabilities Map (v0.1)

This document maps user-facing capabilities to Workbench CLI commands and highlights known gaps.
It is intentionally capability-first rather than a full command catalog.

## Setup and diagnostics

- Initialize repo scaffolding, config, and onboarding flow -> `workbench init` (or just scaffolding via `workbench scaffold`)
- Run an interactive guide for common actions -> `workbench guide`
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
- Open the local browser UI for browsing and editing work items, creating new items, browsing repository documentation/files, and editing local profile settings -> `workbench web`
- Update status and append a note -> `workbench item status`
- Close an item and move it to `work/done` by default -> `workbench item close`
- Delete work items and remove doc backlinks -> `workbench item delete`
- Move or rename items and update inbound links -> `workbench item move`, `workbench item rename`
- Normalize work item front matter lists -> `workbench item normalize`
- Link/unlink specs, ADRs, files, PRs, and issues -> `workbench item link`, `workbench item unlink`
- Regenerate the workboard -> `workbench board regen`

## Docs and knowledge base

- Create or inspect requirement specs -> `workbench spec new`, `workbench spec show`
- Edit, delete, or relink requirement specs -> `workbench spec edit`, `workbench spec delete`, `workbench spec link`, `workbench spec unlink`
- Sync spec front matter and work-item backlinks -> `workbench spec sync`
- Open the browser-based Specs editor to browse, create, and edit policy-aware specs -> `workbench web`
- Create docs in `overview/`, `decisions/`, `runbooks/`, `contracts/`, and related roots with front matter/backlinks -> `workbench doc new`
- Delete docs and remove work item links -> `workbench doc delete`
- Link/unlink docs to work items -> `workbench doc link`, `workbench doc unlink`
- Sync doc front matter and backlinks -> `workbench doc sync` (or `workbench sync --docs`)
- Summarize doc changes into change notes (AI) -> `workbench doc summarize`
- Regenerate navigation indexes and optionally workboard -> `workbench nav sync` (or `workbench sync --nav`)

## GitHub and promotion flows

- Create a PR from a work item -> `workbench github pr create`
- Promote a task into a branch/commit/PR flow -> `workbench promote`
- Run full repo sync (items + docs + nav) -> `workbench sync`
- Run one-shot coherence migration for repository structure and indexes -> `workbench migrate coherent-v1`

## Validation

- Validate schemas, links, and IDs -> `workbench validate` (alias: `workbench verify`)

## Gaps and missing capabilities

- No CLI command to create or close GitHub issues directly (import/sync only).
- No CLI command to delete or close GitHub PRs (linking only).
- No bulk operations for status updates, relinking, or mass edits.
