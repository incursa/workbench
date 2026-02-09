---
workbench:
  type: doc
  workItems: []
  codeRefs: []
owner: platform
status: active
updated: 2025-12-27
---

# Command Output Contracts

All JSON output follows a consistent envelope. Table output is human-readable and not specified here.

## Common envelope

Success:
```json
{
  "ok": true,
  "data": {}
}
```

Success with warnings:
```json
{
  "ok": true,
  "data": {},
  "warnings": [ { "code": "WBWxx", "message": "...", "details": {} } ]
}
```

Error:
```json
{
  "ok": false,
  "error": { "code": "WBxxx", "message": "...", "details": {} }
}
```

## Shared shapes

WorkItem:
```json
{
  "id": "TASK-0042",
  "type": "task",
  "status": "ready",
  "title": "Add promotion workflow",
  "priority": "high",
  "owner": null,
  "created": "2025-12-25",
  "updated": null,
  "tags": ["docs", "workflow"],
  "related": {
    "specs": ["/docs/10-product/features/feature.work-items.md"],
    "adrs": [],
    "files": [],
    "prs": [],
    "issues": [],
    "branches": []
  },
  "slug": "add-promotion-workflow",
  "path": "docs/70-work/items/TASK-0042-add-promotion-workflow.md"
}
```

ValidationMessage:
```json
{
  "code": "WB040",
  "message": "Work item ID does not match file name prefix",
  "path": "docs/70-work/items/TASK-0001-bad-name.md",
  "details": { "expectedPrefix": "TASK-0001" }
}
```

## workbench version

Data:
```json
{ "version": "0.1.0" }
```

## workbench doctor

Data:
```json
{
  "repoRoot": "/abs/path/to/repo",
  "checks": [
    { "name": "git", "status": "ok", "details": { "version": "2.43.0" } },
    { "name": "repo", "status": "ok" },
    { "name": "config", "status": "warn", "details": { "path": ".workbench/config.json" } },
    { "name": "paths", "status": "warn", "details": { "missing": ["docs/70-work/items"] } },
    { "name": "github", "status": "warn", "details": { "reason": "missing token" } }
  ]
}
```

Notes:
- GitHub is `warn` when tokens are missing (Octokit) or `gh` is unauthenticated.

## workbench scaffold

Data:
```json
{
  "created": ["docs/README.md", "docs/70-work/README.md"],
  "skipped": ["docs/10-product/README.md"],
  "configPath": ".workbench/config.json"
}
```

## workbench config show

Data:
```json
{
  "config": { "paths": {}, "ids": {}, "git": {}, "github": {} },
  "sources": {
    "defaults": true,
    "repoConfig": ".workbench/config.json",
    "cliOverrides": []
  }
}
```

## workbench config set

Data:
```json
{
  "path": ".workbench/config.json",
  "config": { "paths": {}, "ids": {}, "git": {}, "github": {} },
  "changed": true
}
```

## workbench config credentials set

Data:
```json
{
  "path": ".workbench/credentials.env",
  "key": "WORKBENCH_AI_OPENAI_KEY",
  "created": false,
  "updated": true,
  "removed": false
}
```

## workbench config credentials unset

Data:
```json
{
  "path": ".workbench/credentials.env",
  "key": "WORKBENCH_AI_OPENAI_KEY",
  "created": false,
  "updated": false,
  "removed": true
}
```

## workbench item new

Data:
```json
{ "item": { /* WorkItem */ } }
```

## workbench item generate

Data:
```json
{ "item": { /* WorkItem */ } }
```

## workbench item import

Data:
```json
{
  "items": [
    {
      "issue": {
        "repo": "org/repo",
        "number": 42,
        "url": "https://github.com/org/repo/issues/42",
        "title": "Add docs",
        "state": "OPEN",
        "labels": ["docs"],
        "pullRequests": ["https://github.com/org/repo/pull/100"]
      },
      "item": { /* WorkItem */ }
    }
  ]
}
```

## workbench item sync

By default, syncs linked work items and creates missing issues; use `--import-issues` to scan GitHub for unlinked issues.

Data:
```json
{
  "imported": [
    {
      "issue": {
        "repo": "org/repo",
        "number": 42,
        "url": "https://github.com/org/repo/issues/42",
        "title": "Add docs",
        "state": "OPEN",
        "labels": ["docs"],
        "pullRequests": []
      },
      "item": { /* WorkItem */ }
    }
  ],
  "issuesCreated": [
    { "itemId": "TASK-0042", "issueUrl": "https://github.com/org/repo/issues/99" }
  ],
  "issuesUpdated": [
    { "itemId": "TASK-0042", "issueUrl": "https://github.com/org/repo/issues/42" }
  ],
  "itemsUpdated": [
    { "itemId": "TASK-0042", "issueUrl": "https://github.com/org/repo/issues/42" }
  ],
  "branchesCreated": [
    { "itemId": "TASK-0042", "branch": "TASK-0042-add-docs" }
  ],
  "warnings": [
    "Issue not found: org/repo#183"
  ],
  "dryRun": false
}
```

## workbench item list

Data:
```json
{
  "items": [
    {
      "id": "TASK-0001",
      "type": "task",
      "status": "ready",
      "title": "Add scaffold command",
      "path": "docs/70-work/items/TASK-0001-add-scaffold-command.md"
    }
  ],
  "counts": { "total": 1, "byStatus": { "ready": 1 } }
}
```

## workbench item show

Data:
```json
{ "item": { /* WorkItem */ } }
```

## workbench item status

Data:
```json
{
  "item": { /* WorkItem */ },
  "noteAppended": true
}
```

## workbench item close

Data:
```json
{
  "item": { /* WorkItem */ },
  "moved": false,
  "newPath": null
}
```

## workbench item delete

Data:
```json
{
  "item": { /* WorkItem */ },
  "docsUpdated": 2
}
```

## workbench item normalize

Data:
```json
{
  "itemsUpdated": 3,
  "dryRun": false
}
```

## workbench board regen

Data:
```json
{
  "path": "docs/70-work/README.md",
  "counts": { "draft": 1, "ready": 2, "in-progress": 1, "blocked": 0 }
}
```

## workbench promote

Data:
```json
{
  "item": { /* WorkItem */ },
  "branch": "work/TASK-0042-add-promotion-workflow",
  "commit": { "sha": "abc123", "message": "Promote TASK-0042: Add promotion workflow" },
  "pushed": false
}
```

## workbench github pr create

Deprecated: `workbench pr create` (use `workbench github pr create`).

Data:
```json
{
  "item": { /* WorkItem */ },
  "pr": { "url": "https://github.com/org/repo/pull/42", "number": 42, "base": "main" }
}
```

## workbench codex doctor

Data:
```json
{
  "available": true,
  "version": "codex 0.XX.X",
  "error": null
}
```

## workbench codex run

Data:
```json
{
  "started": true,
  "terminal": false,
  "exitCode": 0,
  "stdout": "…",
  "stderr": ""
}
```

## workbench worktree start

Data:
```json
{
  "branch": "feature/ticket-113-payment-retry",
  "worktreePath": "C:/src/repo.worktrees/feature/ticket-113-payment-retry",
  "reused": false,
  "codexLaunched": true
}
```

## workbench llm help

Text output:
- Complete AI-oriented command reference in one stream
- Includes global options, command tree, and detailed arguments/options for all commands

## workbench validate

Data:
```json
{
  "errors": [ { /* ValidationMessage */ } ],
  "warnings": [ { /* ValidationMessage */ } ],
  "counts": { "errors": 1, "warnings": 0 }
}
```

## workbench nav sync

Data:
```json
{
  "docsUpdated": 1,
  "itemsUpdated": 1,
  "indexFilesUpdated": 2,
  "workboardUpdated": 1,
  "missingDocs": [],
  "missingItems": [],
  "warnings": []
}
```

## workbench doc delete

Data:
```json
{
  "path": "docs/10-product/payment-flow.md",
  "itemsUpdated": 1
}
```

## workbench sync

Data:
```json
{
  "items": { /* ItemSyncData */ },
  "docs": { /* DocSyncData */ },
  "nav": { /* NavSyncData */ },
  "dryRun": false
}
```
