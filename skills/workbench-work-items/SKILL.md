---
name: workbench-work-items
description: Work item management for Workbench CLI. Use when creating, updating, linking, or closing work items and tracking execution status.
---

## Key settings

- [`.workbench/config.json`](../../.workbench/config.json): paths.itemsDir, paths.workItemsSpecsDir, ids.width, git.branchPattern.
- Status values: planned, in_progress, blocked, complete, cancelled, superseded.

## Core workflows

1. Ensure planning artifacts exist (specifications, architecture docs, and verification artifacts) before major work.
2. Create a work item and set its initial status.
3. Link related requirements, architecture docs, verification artifacts, files, PRs, or issues.
4. Update status and close work items when complete.

## Commands

Create a task:
```bash
workbench item new --type work_item --title "Do the thing" --status planned --priority medium --owner platform
```

Update status:
```bash
workbench item status WI-WB-0001 in-progress --note "started implementation"
```

Close the item:
```bash
workbench item close WI-WB-0001
```

Link docs or PRs:
```bash
workbench item link WI-WB-0001 --spec /specs/requirements/CLI/SPEC-CLI-ONBOARDING.md --pr https://github.com/org/repo/pull/1
```

## Output

- Work items in `specs/work-items/`.
- Linked docs and external artifacts tracked in front matter.

## Guardrails

- Use a work item for every meaningful change and link the supporting docs.
- Keep status accurate so reports and boards stay reliable.
- If a design decision happens during work, capture it in an architecture artifact and link it.
