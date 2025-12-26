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
    "issues": []
  },
  "slug": "add-promotion-workflow",
  "path": "work/items/TASK-0042-add-promotion-workflow.md"
}
```

ValidationMessage:
```json
{
  "code": "WB040",
  "message": "Work item ID does not match file name prefix",
  "path": "work/items/TASK-0001-bad-name.md",
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
    { "name": "paths", "status": "warn", "details": { "missing": ["work/items"] } },
    { "name": "gh", "status": "skip", "details": { "reason": "not installed" } }
  ]
}
```

## workbench scaffold

Data:
```json
{
  "created": ["docs/README.md", "work/WORKBOARD.md"],
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

## workbench item new

Data:
```json
{ "item": { /* WorkItem */ } }
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
      "path": "work/items/TASK-0001-add-scaffold-command.md"
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

## workbench board regen

Data:
```json
{
  "path": "work/WORKBOARD.md",
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

## workbench pr create

Data:
```json
{
  "item": { /* WorkItem */ },
  "pr": { "url": "https://github.com/org/repo/pull/42", "number": 42, "base": "main" }
}
```

## workbench validate

Data:
```json
{
  "errors": [ { /* ValidationMessage */ } ],
  "warnings": [ { /* ValidationMessage */ } ],
  "counts": { "errors": 1, "warnings": 0 }
}
```
