---
id: TASK-0002
type: task
status: draft
priority: medium
owner: platform
created: 2025-12-27
updated: null
tags: null
related:
  specs:
    - </docs/10-product/feature-spec-work-item-sync.md>
  adrs: null
  files: null
  prs: null
  issues: null
title: Sync work items with GitHub issues and branches
---

# TASK-0002 - Sync work items with GitHub issues and branches

## Summary

Add a sync command that keeps local work items and GitHub issues aligned
without deletes. Sync should create missing issues or local work items and
optionally create branches for items that need them.

## Acceptance criteria
- Sync is two-way and never deletes local work items or GitHub issues.
- If a local work item is missing a GitHub issue, sync creates it.
- If a GitHub issue is missing a local work item, sync creates it.
- Sync can create a branch for an item when one is missing.
- Work item metadata records the GitHub issue reference and branch name.

## Proposed metadata layout

Store sync metadata under `related` for consistency with existing work items:

```yaml
related:
  issues:
    - gh:owner/repo#123
  prs: null
  files: null
  adrs: null
  specs: null
  branches:
    - TASK-0002-short-title
```

Notes:
- Use `gh:owner/repo#123` to avoid ambiguity across repos.
- If multiple repos are supported, allow multiple issue refs.
