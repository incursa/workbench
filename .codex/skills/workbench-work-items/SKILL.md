---
name: workbench-work-items
description: Work item management for Workbench CLI. Use when creating, updating, linking, or closing work items and tracking execution status.
---

## Key settings

- [`.workbench/config.json`](../../../.workbench/config.json): `Workflow.Level`, `Paths.ItemsDir`, `Ids.Width`, and `Git.BranchPattern`.
- Status values: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, `superseded`.

## Core workflows

1. Run `workbench context` to resolve the repository policy and next action.
2. Under `light`, skip a work item for trivial or emergency work when durable coordination is unnecessary.
3. Under `standard`, create a work item for meaningful multi-step or handoff work.
4. Under `governed`, link the work item to requirements, architecture, and verification.
5. Finish tracked work with the idempotent `workbench finish` command.

## Commands

Create a task:
```bash
dotnet tool run workbench item new --type work_item --title "Do the thing" --status planned --priority medium --owner platform
```

Update status:
```bash
dotnet tool run workbench item status WI-WB-0001 in_progress --note "started implementation"
```

Finish local reconciliation:
```bash
dotnet tool run workbench finish WI-WB-0001 --pr https://github.com/org/repo/pull/1
```

Link docs or PRs:
```bash
dotnet tool run workbench item link WI-WB-0001 --spec SPEC-WB-EXAMPLE
```

## Output

- Work items in `specs/work-items/<domain>/`.
- Linked canonical artifacts in front matter and completion evidence in the work-item body.

## Guardrails

- Use a work item when the selected workflow policy requires durable execution context.
- Let GitHub and deployment systems remain authoritative for execution state.
- If a decision happens during work, update/create the ADR and link it.
