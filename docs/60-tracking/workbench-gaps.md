---
workbench:
  type: doc
  workItems: []
  codeRefs: []
owner: platform
status: active
updated: 2025-12-27
---

# Workbench Gaps And TODOs

This file tracks missing features, gaps, and decisions to revisit.

## AOT and Trimming

## Work Items
- Enforce optional PR description backlink to work item ID (configurable).
- Decide whether to allow additional work item types beyond bug/task/spike.
- Add hooks installer (pre-commit/pre-push) or CI helper for validation.

## Documentation
- Add doc templates for specs/ADRs with front matter and consistent sections.

## Two-Way Linking
- Decide how to handle multiple work items per doc and multiple docs per work item.
- Define how renames/moves update backlinks across docs and code.

## Docs and CLI
- Update docs to reflect the final linking model and AOT-safe serialization approach.
- Document any new commands, schemas, or config options.
