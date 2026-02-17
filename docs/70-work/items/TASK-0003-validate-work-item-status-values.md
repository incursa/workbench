---
id: TASK-0003
type: task
status: draft
priority: medium
owner: platform
created: 2025-12-27
updated: null
tags: []
related:
  specs: []
  adrs: []
  files: []
  prs: []
  issues:
    - "https://github.com/bravellian/workbench/issues/14"
  branches:
    - TASK-0003-validate-work-item-status-values
title: Validate work item status values
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/70-work/items/TASK-0003-validate-work-item-status-values.md
---

# TASK-0003 - Validate work item status values

## Summary

Prevent setting work item statuses that are not in the allowed set. Add a
future-friendly path for configurable statuses.

## Acceptance criteria
- `workbench item status` rejects invalid status values with a clear error.
- `workbench item new` and `workbench item import` validate status overrides.
- Validation logic is centralized so future configuration can override defaults.
