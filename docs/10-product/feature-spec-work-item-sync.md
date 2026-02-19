---
workbench:
  type: spec
  workItems:
    - TASK-0002
  codeRefs: []
  pathHistory:
    - "C:/docs/10-product/feature-spec-work-item-sync.md"
  path: /docs/10-product/feature-spec-work-item-sync.md
owner: platform
status: draft
updated: 2025-12-27
---

# Feature Spec: Work Item Sync (GitHub Issues + Branches)

## Summary

Add a two-way, non-destructive sync command that keeps local work items and
GitHub issues aligned and can create missing branches when a branch is listed.

## Goals

- Create missing GitHub issues from local work items.
- Create missing local work items from GitHub issues.
- Create missing branches for items that list a branch in `related.branches`.
- Keep metadata aligned without deleting content.

## Non-goals

- Deleting or closing issues automatically.
- Resolving complex merge conflicts in content.
- Managing PR state beyond capturing references.

## User stories / scenarios

- As a user, I can run sync and see missing items created on either side.
- As a user, I can sync and get a branch created for any work item that lists it in `related.branches`.
- As a user, I can sync without fear of losing data.

## Requirements

- Sync is bidirectional and never deletes local work items or GitHub issues.
- If a local work item is missing a GitHub issue, sync creates it.
- If a GitHub issue is missing a local work item, sync creates it.
- Sync can create a branch when one is listed in `related.branches`.
- Record the GitHub issue ref and branch name in work item metadata.
- Support a dry-run mode that reports changes without writing.
- Always import GitHub issues into local work items, even if the issue is closed.
- Skip creating GitHub issues or branches for local items in terminal states.
- For bulk sync, prefer local content when updating existing GitHub issues.
- For ID-scoped sync, allow `--prefer` to choose local or GitHub as source of truth.

## UX notes

- Provide a summary of planned changes before execution.
- Offer a confirmation prompt unless `--yes` or `--non-interactive` is set.
- Print created issue URLs and branch names at the end.

## Dependencies

- GitHub CLI or API credentials for issue creation and queries.
- Work item schema support for `related.branches`.

## Risks and mitigations

- Risk: mismatched titles or bodies produce noisy churn.
  - Mitigation: use stable templates and avoid overwriting by default.
- Risk: repo mismatch when multiple remotes exist.
  - Mitigation: require explicit repo override or use the default remote.

## Related work items

- [TASK-0002](/docs/70-work/done/TASK-0002-promote-existing-work-items-to-github-issues.md)

## Related ADRs

- </docs/40-decisions/ADR-0000-short-title.md>

## Open questions

- Should sync update existing issue titles/bodies or only create missing items?
- How should conflicts be surfaced when both sides changed?
