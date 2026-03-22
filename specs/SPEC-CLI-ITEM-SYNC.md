---
artifact_id: SPEC-CLI-ITEM-SYNC
artifact_type: specification
title: "CLI Item Sync Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-ITEM
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-ITEM-SYNC - CLI Item Sync Command

## Purpose

Define the contract for reconciling local work items with GitHub and branches.

## Scope

- `workbench item sync`

## REQ-CLI-ITEM-SYNC-0001 `workbench item sync`

`item sync` MUST accept the documented ID, issue, prefer, dry-run, and
import-issues options, reconcile local work items with GitHub issues and branch
state, and preserve local content unless the selected source preference says
otherwise.

## REQ-CLI-ITEM-SYNC-0002 Source preference

`item sync` MUST honor `--prefer local` and `--prefer github` when local and
remote descriptions differ.

## REQ-CLI-ITEM-SYNC-0003 Dry-run behavior

`item sync` MUST report reconciliation changes without writing files when
`--dry-run` is set.

## REQ-CLI-ITEM-SYNC-0004 Reconciliation reporting

`item sync` MUST report each reconciled item in its output so users can see
what changed.
