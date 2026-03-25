---
name: workbench-architecture
description: Architecture workflows for Workbench CLI. Use when documenting system design, tradeoffs, or rationale in canonical architecture artifacts.
---

## Key settings

- [`.workbench/config.json`](../../.workbench/config.json): paths.architectureDir, paths.specsRoot, git.defaultBaseBranch.
- Use `workbench config show --format json` to confirm defaults.

## Core workflows

1. Planning phase: create architecture docs for design intent and scope.
2. When a decision affects the design, capture it in an architecture doc instead of a separate decision record.
3. Link architecture docs to work items and specs.

## Commands

Create an architecture doc:
```bash
workbench doc new --type architecture --title "Subsystem overview" --path specs/architecture/WB/ARC-WB-0001-subsystem-overview.md --work-item WI-WB-0001
```

Link an architecture doc to a work item:
```bash
workbench doc link --type architecture --path specs/architecture/WB/ARC-WB-0001-subsystem-overview.md --work-item WI-WB-0001
```

Sync backlinks:
```bash
workbench doc sync --all
```

## Output

- Architecture docs with consistent canonical front matter.
- Work items that reference related specs and architecture docs.

## Guardrails

- Use architecture docs for structure, flows, and design rationale.
- Keep architecture status aligned with the current design state.
- If a significant decision changes the design, update the relevant
  architecture artifact and linked spec rather than creating a separate
  decision record.
