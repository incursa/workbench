---
name: workbench-docs
description: Documentation workflows for Workbench CLI. Use when creating or updating specs, architecture docs, verification docs, runbooks, guides, or general docs, and when syncing backlinks or change notes.
---

## Key settings

- `.workbench/config.json`: paths.docsRoot, paths.templatesDir, ids.width, prefixes, git.defaultBaseBranch, github.owner, github.repository.
- Use `workbench config show --format json` to verify effective settings.

## Core workflows

1. Planning phase: create specs and architecture docs before major changes.
2. Doing phase: keep docs updated as implementation decisions change.
3. Keep doc front matter and backlinks in sync.
4. Summarize doc changes when publishing.

## Commands

Create a doc:
```bash
workbench doc new --type doc --title "Title" --path docs/title.md --work-item WI-WB-0001
```

Create a spec:
```bash
workbench doc new --type spec --title "Title" --path specs/SPEC-EXAMPLE.md --work-item WI-WB-0001
```

Create an architecture doc:
```bash
workbench doc new --type doc --title "System design" --path architecture/system-design.md --work-item WI-WB-0001
```

Create a verification doc:
```bash
workbench doc new --type verification --title "Proof" --path verification/proof.md --work-item WI-WB-0001
```

Link a doc to work items:
```bash
workbench doc link --type spec --path specs/SPEC-EXAMPLE.md --work-item WI-WB-0001
```

Sync front matter and backlinks:
```bash
workbench doc sync --all --dry-run
workbench doc sync --all
```

Summarize doc changes:
```bash
workbench doc summarize --staged --update-index
```

## Output

- Markdown docs with Workbench front matter and backlinks.
- Updated work item references in docs and work items.

## Guardrails

- Keep doc types aligned with folder intent (specs, architecture, verification, runbooks, and general docs).
- Always link docs to relevant work items with `--work-item` or `workbench item link`.
- For major changes, write a spec before implementation.
- Prefer `workbench doc` commands; `workbench spec` and `workbench adr` are deprecated.
