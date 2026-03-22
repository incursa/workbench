---
artifact_id: SPEC-SYNC-WORK-ITEM-SYNC
artifact_type: specification
title: Work Item Sync (GitHub Issues + Branches)
domain: SYNC
capability: work-item-sync
status: draft
owner: platform
related_artifacts:
  - TASK-0002
  - ADR-2026-03-07-repo-native-operating-model
  - ADR-2025-12-28-github-provider-abstraction-and-octokit
---

# SPEC-SYNC-WORK-ITEM-SYNC - Work Item Sync (GitHub Issues + Branches)

## Purpose

Add a two-way, non-destructive sync command that keeps local work items and
GitHub issues aligned and can create missing branches when a branch is listed.

## Scope

- synchronize local work items with GitHub issues in both directions
- create branches when a work item references a branch name
- preserve local content and avoid destructive cleanup
- keep the sync command compatible with the current provider abstraction

## Context

Workbench already treats local work-item files as the canonical repo record, but
users still need a predictable way to pull issue metadata into local files and
push local intent back into GitHub when necessary. The sync command should
repair missing links and create missing records without deleting content.

## REQ-SYNC-0001 Keep sync bidirectional and non-destructive
The sync command MUST keep local work items and GitHub issues aligned in both directions without deleting local work items or GitHub issues.

Trace:
- Implemented By:
  - [TASK-0002](/work/done/TASK-0002-promote-existing-work-items-to-github-issues.md)
- Related:
  - [ADR-2026-03-07-repo-native-operating-model](/docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md)
  - [ADR-2025-12-28-github-provider-abstraction-and-octokit](/docs/40-decisions/ADR-2025-12-28-github-provider-abstraction-and-octokit.md)

Notes:
- preserve content on both sides
- keep the command safe to rerun

## REQ-SYNC-0002 Create missing branches when requested
The sync command MUST create a branch when a work item lists one in `related.branches`.

Trace:
- Implemented By:
  - [TASK-0002](/work/done/TASK-0002-promote-existing-work-items-to-github-issues.md)
- Related:
  - [ADR-2026-03-07-repo-native-operating-model](/docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md)
  - [ADR-2025-12-28-github-provider-abstraction-and-octokit](/docs/40-decisions/ADR-2025-12-28-github-provider-abstraction-and-octokit.md)

Notes:
- record the branch name in work-item metadata
- skip branch creation for terminal items

## REQ-SYNC-0003 Record GitHub references in metadata
The sync command MUST record the GitHub issue reference and branch names in work-item metadata when those records are discovered or created.

Trace:
- Implemented By:
  - [TASK-0002](/work/done/TASK-0002-promote-existing-work-items-to-github-issues.md)
- Related:
  - [ADR-2026-03-07-repo-native-operating-model](/docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md)
  - [ADR-2025-12-28-github-provider-abstraction-and-octokit](/docs/40-decisions/ADR-2025-12-28-github-provider-abstraction-and-octokit.md)

Notes:
- preserve issue URLs and branch links
- keep front matter as the cross-link store

## REQ-SYNC-0004 Support dry-run and source preference
The sync command MUST support dry-run mode and a source preference for ID-scoped sync operations when local or GitHub content needs to be treated as the primary source.

Trace:
- Implemented By:
  - [TASK-0002](/work/done/TASK-0002-promote-existing-work-items-to-github-issues.md)
- Related:
  - [ADR-2026-03-07-repo-native-operating-model](/docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md)
  - [ADR-2025-12-28-github-provider-abstraction-and-octokit](/docs/40-decisions/ADR-2025-12-28-github-provider-abstraction-and-octokit.md)

Notes:
- prefer local content when bulk-updating existing GitHub issues
- allow a `--prefer` choice where the command needs one

## REQ-SYNC-0005 Import closed GitHub issues
The sync command MUST continue to import GitHub issues into local work items even if the issue is closed.

Trace:
- Implemented By:
  - [TASK-0002](/work/done/TASK-0002-promote-existing-work-items-to-github-issues.md)
- Related:
  - [ADR-2026-03-07-repo-native-operating-model](/docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md)
  - [ADR-2025-12-28-github-provider-abstraction-and-octokit](/docs/40-decisions/ADR-2025-12-28-github-provider-abstraction-and-octokit.md)

Notes:
- skip creating GitHub records for local items in terminal states
- preserve closed issue history locally

## Open Questions

- Should sync update existing issue titles and bodies or only create missing items?
- How should conflicts be surfaced when both sides changed?
